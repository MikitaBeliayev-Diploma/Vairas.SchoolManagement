using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Local eligibility, not a reservation or a reusable authorization permit.</summary>
public sealed record EligibilityResponse(Guid EnrollmentId, Guid SchoolId, Guid StudentId, bool Active, string Category, long Version, bool TeacherAllowed);
