using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

public sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class MemoryStore(FixedClock clock) : ISchoolStore
{
    public List<object> Rows { get; set; } = [];
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
public sealed class MemoryActivity : IUserActivity
{
    public List<Guid> Calls { get; } = []; public HashSet<Guid> Inactive { get; } = [];
    public Dictionary<Guid, ProfileBinding> Bindings { get; } = [];
    public Task RequireActiveAsync(Guid user, CancellationToken ct)
    {
        Calls.Add(user);
        if (Inactive.Contains(user))
            throw SchoolException.Denied();
        return Task.CompletedTask;
    }
    public Task<ProfileBinding> BindingAsync(string kind, Guid profile, CancellationToken ct) => Task.FromResult(Bindings.TryGetValue(profile, out var b) ? b : throw SchoolException.Dependency());
}
public abstract class BusinessFixture
{
    protected FixedClock Clock = null!; protected MemoryStore Store = null!; protected MemoryActivity Activity = null!; protected SchoolAccessOptions Access = null!; protected SchoolService Service = null!;
    protected Guid School, AdminUser, Target, Other, Student, Teacher, Group, Enrollment;
    protected SchoolActor Admin => Actor(AdminUser);
    protected SchoolActor Actor(Guid user) => new(user, Student, Teacher, Guid.NewGuid(), Clock.Now.AddSeconds(30));
    [SetUp]
    public void Setup()
    {
        Clock = new();
        Store = new(Clock);
        Activity = new();
        Access = new();
        Service = new(Store, Activity, Access, Clock);
        School = Guid.NewGuid();
        AdminUser = Guid.NewGuid();
        Target = Guid.NewGuid();
        Other = Guid.NewGuid();
        Student = Guid.NewGuid();
        Teacher = Guid.NewGuid();
        Group = Guid.NewGuid();
        Enrollment = Guid.NewGuid();
        Store.Rows.Add(new School { SchoolId = School, Name = "School", Status = SchoolStatus.Active, Revision = 1, CreatedAt = Clock.Now, UpdatedAt = Clock.Now });
        Member(AdminUser, SchoolRole.Administrator);
    }
    protected SchoolMembership Member(Guid user, SchoolRole role)
    {
        var m = new SchoolMembership { SchoolId = School, MembershipId = Guid.NewGuid(), UserId = user, Role = role, StartedAt = Clock.Now.AddDays(-1), Revision = 1 };
        Store.Rows.Add(m);
        return m;
    }
    protected StudentEnrollment Enroll(Guid? student = null)
    {
        var e = new StudentEnrollment { SchoolId = School, EnrollmentId = Enrollment, StudentId = student ?? Student, CategoryCode = "B", Status = EnrollmentStatus.Active, Revision = 1, StartedAt = Clock.Now.AddDays(-1), StatusChangedAt = Clock.Now.AddDays(-1) };
        Store.Rows.Add(e);
        return e;
    }
    protected TrainingGroup TrainingGroup()
    {
        var g = new TrainingGroup { SchoolId = School, GroupId = Group, Name = "Group", StartedAt = Clock.Now.AddDays(-1), Revision = 1, MembershipRevision = 1 };
        Store.Rows.Add(g);
        Store.Rows.Add(new GroupRosterRevision { SchoolId = School, GroupId = Group, MembershipRevision = 1, RecordedAt = Clock.Now });
        return g;
    }
    protected Task<MutationResponse<InvitationResponse>> Issue(SchoolRole role = SchoolRole.Teacher, Guid? operation = null) => Service.IssueAsync(School, operation ?? Guid.NewGuid(), new(Target, role, role == SchoolRole.Student ? "B" : null), Admin, default);
    protected static SchoolException Error(TestDelegate action, string code)
    {
        var e = Assert.Throws<SchoolException>(action)!;
        Assert.That(e.Code, Is.EqualTo(code));
        return e;
    }
    protected static SchoolException Error(AsyncTestDelegate action, string code)
    {
        var e = Assert.ThrowsAsync<SchoolException>(action)!;
        Assert.That(e.Code, Is.EqualTo(code));
        return e;
    }
}
