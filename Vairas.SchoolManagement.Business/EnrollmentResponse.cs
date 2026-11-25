using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>One school-specific enrollment; no learning hours or programme ownership.</summary>
public sealed record EnrollmentResponse(Guid EnrollmentId, Guid SchoolId, Guid StudentId, string LicenseCategory, EnrollmentStatus Status, long Version, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, Guid? GroupId = null, Guid? ProgramVersionId = null);
