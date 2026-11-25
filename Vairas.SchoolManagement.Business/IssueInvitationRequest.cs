using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Issue a target-bound invitation; server controls expiry and issuer.</summary>
public sealed record IssueInvitationRequest(Guid TargetUserId, SchoolRole Role, string? CategoryCode = null);
