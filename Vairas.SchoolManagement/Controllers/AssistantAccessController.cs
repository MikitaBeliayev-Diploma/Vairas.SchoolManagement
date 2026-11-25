using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Controllers;

[Route("internal/v1/assistant/access"), Authorize(Policy = "school.assistant.read")]
public sealed class AssistantAccessController(IAssistantAccessService service) : BaseApiController
{
    /// <summary>Read the current membership epoch without chat contents or membership listings.</summary>
    [HttpPost(Name = "getAssistantAccess")]
    public async Task<ActionResult<AssistantAccessResponse>> Read(AssistantAccessRequest request, CancellationToken ct) => Ok(await service.ReadAsync(request, ct));
}
