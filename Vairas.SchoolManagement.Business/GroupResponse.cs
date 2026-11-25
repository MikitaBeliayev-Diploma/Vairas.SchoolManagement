using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Group metadata. MembershipVersion selects a retained roster basis.</summary>
public sealed record GroupResponse(Guid GroupId, Guid SchoolId, Guid? BranchId, string Name, string? CategoryCode, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long Revision, long MembershipVersion);
