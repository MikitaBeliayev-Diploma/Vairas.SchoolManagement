using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolStore
{
    Task<ISchoolSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DiscoverAsync(Guid userId, Guid? studentId, CancellationToken ct);
}
