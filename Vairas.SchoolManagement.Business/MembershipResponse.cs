using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>School-local role; a global profile alone grants no authority.</summary>
public sealed record MembershipResponse(Guid MembershipId, Guid UserId, SchoolRole Role, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long Revision);
