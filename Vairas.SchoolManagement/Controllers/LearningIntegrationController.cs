using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Controllers;

[Route("internal/v1/learning"), Authorize(Policy = "school.learning.read")]
public sealed class LearningIntegrationController(ISchoolLearningService service) : BaseApiController
{
    /// <summary>Current scoped Learning authority or read-only integration metadata; no membership is created.</summary>
    [HttpPost("authority", Name = "getLearningAuthority")]
    public async Task<ActionResult<LearningAuthorityResponse>> Authority(LearningAuthorityRequest request, CancellationToken ct) => Ok(await service.AuthorityAsync(request, ct));
    /// <summary>Finite School-owned resources for Learning curriculum demand construction.</summary>
    [HttpPost("planning-resources", Name = "getLearningPlanningResources")]
    public async Task<ActionResult<LearningPlanningResourcesResponse>> Resources(LearningPlanningResourcesRequest request, CancellationToken ct) => Ok(await service.ResourcesAsync(request, ct));
}
