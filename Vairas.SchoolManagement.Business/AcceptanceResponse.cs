using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Canonical result of accepting one invitation.</summary>
public sealed record AcceptanceResponse(Guid OperationId, Guid SchoolId, Guid InvitationId, long InvitationRevision, OperationStatus OperationStatus, string Outcome, bool LocalApplied, Guid? ResultMembershipId, Guid? ResultEnrollmentId);
