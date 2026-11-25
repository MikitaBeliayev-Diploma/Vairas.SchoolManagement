namespace Vairas.SchoolManagement.Business;

public interface ISchoolLearningService
{
    Task<LearningAuthorityResponse> AuthorityAsync(LearningAuthorityRequest request, CancellationToken ct);
    Task<LearningPlanningResourcesResponse> ResourcesAsync(LearningPlanningResourcesRequest request, CancellationToken ct);
}
