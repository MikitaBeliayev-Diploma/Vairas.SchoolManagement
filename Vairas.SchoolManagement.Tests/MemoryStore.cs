using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

public sealed class MemoryStore(FixedClock clock) : ISchoolStore
{
    public List<object> Rows { get; set; } = [];
    public List<(Guid School, int Take, int Returned)> InvitationPages { get; } = [];
    public int Commits
    {
        get; private set;
    }
    public Action? OnOpen
    {
        get; set;
    }
    public Task<ISchoolSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct)
    {
        OnOpen?.Invoke();
        return Task.FromResult<ISchoolSession>(new Session(this, clock));
    }
    public Task<IReadOnlyList<Guid>> DiscoverAsync(Guid user, Guid? student, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>(Rows.OfType<SchoolInvitation>().Where(x => x.TargetUserId == user).Select(x => x.SchoolId)
     .Concat(Rows.OfType<SchoolMembership>().Where(x => x.UserId == user).Select(x => x.SchoolId))
     .Concat(Rows.OfType<StudentEnrollment>().Where(x => x.StudentId == student).Select(x => x.SchoolId)).Distinct().ToArray());
    private sealed class Session : ISchoolSession
    {
        private readonly MemoryStore store; private readonly FixedClock clock; private readonly List<object> rows;
        public Session(MemoryStore store, FixedClock clock)
        {
            this.store = store;
            this.clock = clock;
            rows = store.Rows.Select(x => JsonSerializer.Deserialize(JsonSerializer.Serialize(x, x.GetType()), x.GetType())!).ToList();
        }
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> p, CancellationToken ct) where T : class => Task.FromResult(rows.OfType<T>().SingleOrDefault(p.Compile()));
        public Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> p, CancellationToken ct) where T : class => Task.FromResult<IReadOnlyList<T>>(rows.OfType<T>().Where(p.Compile()).ToArray());
        public Task<IReadOnlyList<SchoolInvitation>> ListOwnInvitationsAsync(Guid schoolId, Guid targetUserId, int take, InvitationCursor? after, CancellationToken ct)
        {
            var page = rows.OfType<SchoolInvitation>().Where(x => x.SchoolId == schoolId && x.TargetUserId == targetUserId)
                .Where(x => after is null || x.CreatedAt > after.Created || x.CreatedAt == after.Created &&
                    (x.SchoolId.CompareTo(after.School) > 0 || x.SchoolId == after.School && x.InvitationId.CompareTo(after.Id) > 0))
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.SchoolId).ThenBy(x => x.InvitationId).Take(take).ToArray();
            store.InvitationPages.Add((schoolId, take, page.Length));
            return Task.FromResult<IReadOnlyList<SchoolInvitation>>(page);
        }
        public Task<IReadOnlyList<SchoolInvitation>> ListSchoolInvitationsAsync(Guid schoolId, int take, InvitationCursor? after, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SchoolInvitation>>(rows.OfType<SchoolInvitation>().Where(x => x.SchoolId == schoolId)
                .Where(x => after is null || x.CreatedAt > after.Created || x.CreatedAt == after.Created && x.InvitationId.CompareTo(after.Id) > 0)
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.InvitationId).Take(take).ToArray());
        public Task<DateTimeOffset> NowAsync(CancellationToken ct) => Task.FromResult(clock.Now);
        public void Add<T>(T row) where T : class => rows.Add(row);
        public Task CommitAsync(CancellationToken ct)
        {
            store.Rows = rows;
            store.Commits++;
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
