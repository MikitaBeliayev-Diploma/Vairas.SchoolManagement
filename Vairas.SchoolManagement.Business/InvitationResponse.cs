using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Exact target-bound invitation with derived expiration and activation state.</summary>
public sealed record InvitationResponse(Guid InvitationId, Guid SchoolId, Guid TargetUserId, SchoolRole Role, string? CategoryCode, long Revision,
 DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, InvitationStatus Status, string EffectiveStatus, string AcceptanceState,
 DateTimeOffset? AcceptedAt, DateTimeOffset? RevokedAt, Guid? AcceptedOperationId, Guid? ResultMembershipId, Guid? ResultEnrollmentId);
