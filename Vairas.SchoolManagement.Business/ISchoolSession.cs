using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolSession : IAsyncDisposable
{
    Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    Task<IReadOnlyList<SchoolInvitation>> ListOwnInvitationsAsync(Guid schoolId, Guid targetUserId, int take, InvitationCursor? after, CancellationToken ct);
    Task<IReadOnlyList<SchoolInvitation>> ListSchoolInvitationsAsync(Guid schoolId, int take, InvitationCursor? after, CancellationToken ct);
    Task<DateTimeOffset> NowAsync(CancellationToken ct);
    void Add<T>(T entity) where T : class;
    Task CommitAsync(CancellationToken ct);
}
