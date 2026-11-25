using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface IUserActivity
{
    Task RequireActiveAsync(Guid userId, CancellationToken ct); Task<ProfileBinding> BindingAsync(string kind, Guid profileId, CancellationToken ct);
    /// <summary>Resolve a global user's role identifiers for deactivation reconciliation (profiles.binding.read).</summary>
    Task<UserRoleProfiles> RoleProfilesAsync(Guid userId, CancellationToken ct) => throw SchoolErrors.Dependency();
}
