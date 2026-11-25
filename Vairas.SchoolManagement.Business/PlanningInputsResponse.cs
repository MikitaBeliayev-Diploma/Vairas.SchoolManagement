using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Missing planning dependencies; absent providers never imply completeness.</summary>
public sealed record PlanningInputsResponse(Guid SchoolId, bool Complete, IReadOnlyList<string> MissingItems)
{
    public IReadOnlyList<EnrollmentPlanningInputsResponse> Enrollments { get; init; } = [];
}
