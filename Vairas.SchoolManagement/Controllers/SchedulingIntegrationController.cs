using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;
[Route("internal/v1/scheduling")]
public sealed class SchedulingIntegrationController(ISchoolSchedulingService service) : BaseApiController
{
    /// <summary>Current School authorization, exact interval and roster for Scheduling.</summary>
    [HttpPost("snapshots"),Authorize(Policy="school.scheduling.read")]
    public async Task<ActionResult<SchedulingSnapshot>> Snapshot(SchedulingSnapshotRequest request,CancellationToken ct)=>Ok(await service.SnapshotAsync(request,ct));
    /// <summary>Current school membership and explicit special permission evidence.</summary>
    [HttpGet("access"),Authorize(Policy="school.scheduling.read")]
    public async Task<ActionResult<SchedulingSnapshot>> Access(Guid schoolId,Guid actorUserId,CancellationToken ct)=>Ok(await service.AccessAsync(schoolId,actorUserId,ct));
    /// <summary>Authorize the exact School rule resource scope.</summary>
    [HttpPost("rule-scope"),Authorize(Policy="school.scheduling.read")]
    public async Task<ActionResult<RuleScopeResponse>> RuleScope(RuleScopeRequest request,CancellationToken ct)=>Ok(await service.RuleScopeAsync(request,ct));
    /// <summary>Return the durable local decision for Scheduling finalize/abort reconciliation.</summary>
    [HttpGet("coordination/{operationId:guid}"),Authorize(Policy="school.coordination.read")]
    public async Task<ActionResult<SchedulingDecision>> Decision(Guid operationId,Guid schoolId,CancellationToken ct)=>Ok(await service.DecisionAsync(schoolId,operationId,ct));
}
