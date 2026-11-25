using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;

[Authorize(Policy = "SchoolEntry"), Route("api/v1/schools/{schoolId:guid}/invitations")]
public sealed class InvitationsController(ISchoolService school, ISchoolActorFactory actors) : BaseApiController
{
    /// <summary>List invitation metadata for the current school administrator; secrets are never returned.</summary>
    [HttpGet(Name = "listSchoolInvitations")]
    public async Task<ActionResult<InvitationListResponse>> List(Guid schoolId, int? limit, string? cursor, CancellationToken ct) =>
        Ok(await school.SchoolInvitationsAsync(schoolId, limit ?? 20, cursor, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Issue a 24-hour invitation to an exact UserId, with no membership activation.</summary>
    [HttpPost(Name = "issueInvitation")]
    public async Task<ActionResult<MutationResponse<InvitationResponse>>> Issue(Guid schoolId, IssueInvitationRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        var result = await school.IssueAsync(schoolId, operation, request, await actors.CreateAsync(HttpContext, ct), ct);
        return result.Replayed ? Ok(result) : Created($"/api/v1/schools/{schoolId:D}/invitations/{result.Resource.InvitationId:D}", result);
    }
    /// <summary>Read an invitation as its target or a currently authorized issuer.</summary>
    [HttpGet("{invitationId:guid}", Name = "getInvitation")]
    public async Task<ActionResult<InvitationResponse>> Get(Guid schoolId, Guid invitationId, CancellationToken ct) => Ok(await school.InvitationAsync(schoolId, invitationId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Accept as the exact target; new effective membership/enrollment returns 503 before effects until Scheduling gate exists.</summary>
    [HttpPost("{invitationId:guid}/accept", Name = "acceptInvitation")]
    public async Task<ActionResult<AcceptanceResponse>> Accept(Guid schoolId, Guid invitationId, AcceptInvitationRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct) => Ok(await school.AcceptAsync(schoolId, invitationId, operation, request, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Revoke Pending invitation atomically; accepted membership is never compensated here.</summary>
    [HttpPost("{invitationId:guid}/revoke", Name = "revokeInvitation")]
    public async Task<ActionResult<MutationResponse<InvitationResponse>>> Revoke(Guid schoolId, Guid invitationId, RevokeInvitationRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct) => Ok(await school.RevokeAsync(schoolId, invitationId, operation, request, await actors.CreateAsync(HttpContext, ct), ct));
}

