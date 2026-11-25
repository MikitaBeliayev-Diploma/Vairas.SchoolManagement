using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;

[Authorize(Policy = "SchoolEntry"), Route("api/v1/users/me")]
public sealed class MySchoolController(ISchoolService school, ISchoolActorFactory actors) : BaseApiController
{
    /// <summary>Discover active schools and current local roles of this user.</summary>
    [HttpGet("schools", Name = "getMySchools")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceResponse>>> Schools(CancellationToken ct)
    {
        if (Vairas.Delegation.ReadPreviewDelegation.Marked(User)) return Forbid();
        return Ok(await school.WorkspacesAsync(await actors.CreateAsync(HttpContext, ct), ct));
    }
    /// <summary>List only invitations addressed to the authenticated global UserId.</summary>
    [HttpGet("invitations", Name = "getMyInvitations")]
    public async Task<ActionResult<InvitationListResponse>> Invitations(Guid? schoolId, int? limit, string? cursor, CancellationToken ct) => Ok(await school.MyInvitationsAsync(schoolId, limit ?? 20, cursor, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Read only the caller's enrollments, independently across schools and categories.</summary>
    [HttpGet("enrollments", Name = "getMyEnrollments")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentResponse>>> Enrollments(CancellationToken ct)
    {
        var result = await school.MyEnrollmentsAsync(await actors.CreateAsync(HttpContext, ct), ct);
        if (Vairas.Delegation.ReadPreviewDelegation.Marked(User))
        {
            var scope = Vairas.Delegation.ReadPreviewDelegation.Parse(User, 2);
            return Ok(result.Where(e => e.SchoolId == scope.School).ToArray());
        }
        return Ok(result);
    }
}
