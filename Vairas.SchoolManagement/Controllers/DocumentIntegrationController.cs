using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Controllers;

[Route("internal/v1/documents"), Authorize(Policy = "school.documents.read")]
public sealed class DocumentIntegrationController(ISchoolDocumentService service) : BaseApiController
{
    /// <summary>Read exact current school/enrollment authority and minimal rendering metadata.</summary>
    [HttpPost("authority", Name = "getDocumentAuthority")]
    public async Task<ActionResult<DocumentAuthorityResponse>> Authority(DocumentAuthorityRequest request, CancellationToken ct) => Ok(await service.AuthorityAsync(request,ct));
}
