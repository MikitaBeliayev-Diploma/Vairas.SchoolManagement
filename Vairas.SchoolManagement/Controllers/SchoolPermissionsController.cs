using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;

[Authorize(Policy="SchoolEntry"),Route("api/v1/schools/{schoolId:guid}")]
public sealed class SchoolPermissionsController(ISchoolSchedulingService service,ISchoolActorFactory actors) : BaseApiController
{
    /// <summary>Platform operator grants SchoolRuleOwner to an Administrator or Assessor to a Teacher.</summary>
    [HttpPost("memberships/{membershipId:guid}/permissions")]
    public async Task<ActionResult<SchoolChangeResponse>> Grant(Guid schoolId,Guid membershipId,GrantPermissionRequest request,[FromHeader(Name="Idempotency-Key")]Guid operation,CancellationToken ct)=>
        Ok(await service.ChangeAsync(schoolId,operation,new("GrantPermission",membershipId,Permission:request.Permission),await actors.CreateAsync(HttpContext,ct),ct));
    /// <summary>Revoke an exact permission grant with an accountable reason.</summary>
    [HttpPost("permission-grants/{grantId:guid}/revocation")]
    public async Task<ActionResult<SchoolChangeResponse>> Revoke(Guid schoolId,Guid grantId,RevokePermissionRequest request,[FromHeader(Name="Idempotency-Key")]Guid operation,CancellationToken ct)
    {SchoolRules.Text(request.Reason,500);return Ok(await service.ChangeAsync(schoolId,operation,new("RevokePermission",grantId,Reason:request.Reason),await actors.CreateAsync(HttpContext,ct),ct));}
    /// <summary>Read only the caller's coordination outcome or a configured operator's scope.</summary>
    [HttpGet("activation-operations/{operationId:guid}")]
    public async Task<ActionResult<SchoolChangeResponse>> Outcome(Guid schoolId,Guid operationId,CancellationToken ct)=>Ok(await service.OutcomeAsync(schoolId,operationId,await actors.CreateAsync(HttpContext,ct),ct));
    /// <summary>Platform operator aborts only an intent with no local School commit; unknown Scheduling outcomes retain protection.</summary>
    [HttpPost("activation-operations/{operationId:guid}/abort")]
    public async Task<ActionResult<SchoolChangeResponse>> Abort(Guid schoolId,Guid operationId,RevokePermissionRequest request,CancellationToken ct)=>Ok(await service.AbortAsync(schoolId,operationId,request.Reason,await actors.CreateAsync(HttpContext,ct),ct));
    /// <summary>Remove one group association through the same durable reservation protocol.</summary>
    [HttpPost("enrollments/{enrollmentId:guid}/group-removal")]
    public async Task<ActionResult<SchoolChangeResponse>> RemoveGroup(Guid schoolId,Guid enrollmentId,GroupAssignmentRequest request,[FromHeader(Name="Idempotency-Key")]Guid operation,CancellationToken ct)=>
        Ok(await service.ChangeAsync(schoolId,operation,new("RemoveGroup",enrollmentId,request.ExpectedRevision,GroupId:request.GroupId),await actors.CreateAsync(HttpContext,ct),ct));
}
