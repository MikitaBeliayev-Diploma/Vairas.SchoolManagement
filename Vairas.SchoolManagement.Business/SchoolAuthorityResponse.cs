using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Exact binding echo; validation grants no membership and consumes no invitation.</summary>
public sealed record SchoolAuthorityResponse(bool Allowed, Guid ActorUserId, Guid TargetUserId, Guid SchoolId, Guid? InvitationId, SchoolRole Role, bool Bootstrap, string AuthorityBasis, DateTimeOffset ValidUntil, Guid? IssuerUserId = null);
