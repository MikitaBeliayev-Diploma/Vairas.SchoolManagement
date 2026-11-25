using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Read-only staff provisioning request compatible with existing User Management.</summary>
public sealed record SchoolAuthorityRequest(Guid ActorUserId, Guid TargetUserId, Guid SchoolId, Guid? InvitationId, SchoolRole Role, bool Bootstrap);
