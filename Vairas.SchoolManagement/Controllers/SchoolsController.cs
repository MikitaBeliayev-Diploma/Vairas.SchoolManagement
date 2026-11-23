using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;

[ApiController, Authorize(Policy = "SchoolEntry"), Route("api/v1/schools/{schoolId:guid}")]
public sealed class SchoolsController(ISchoolService school, ISchoolActorFactory actors) : ControllerBase
{
    /// <summary>Read school metadata under current membership or configured operator authority.</summary>
    [HttpGet(Name = "getSchool")]
    public async Task<ActionResult<SchoolResponse>> Get(Guid schoolId, CancellationToken ct) => Ok(await school.GetSchoolAsync(schoolId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Rename a school without changing eligibility.</summary>
    [HttpPatch("name", Name = "renameSchool")]
    public async Task<ActionResult<MutationResponse<SchoolResponse>>> Rename(Guid schoolId, RenameRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct) =>
     Ok(await school.RenameSchoolAsync(schoolId, operation, request, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>List branches in the authorized school.</summary>
    [HttpGet("branches", Name = "listBranches")]
    public async Task<ActionResult<IReadOnlyList<BranchResponse>>> Branches(Guid schoolId, CancellationToken ct) => Ok(await school.BranchesAsync(schoolId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Read one same-school branch.</summary>
    [HttpGet("branches/{branchId:guid}", Name = "getBranch")]
    public async Task<ActionResult<BranchResponse>> Branch(Guid schoolId, Guid branchId, CancellationToken ct) => Ok(await school.BranchAsync(schoolId, branchId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Create a branch; this grants no membership or scheduling rights.</summary>
    [HttpPost("branches", Name = "createBranch")]
    public async Task<ActionResult<MutationResponse<BranchResponse>>> CreateBranch(Guid schoolId, CreateBranchRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        var result = await school.SaveBranchAsync(schoolId, null, operation, request, null, await actors.CreateAsync(HttpContext, ct), ct);
        return Mutation(result, $"/api/v1/schools/{schoolId:D}/branches/{result.Resource.BranchId:D}");
    }
    /// <summary>Update branch display/address metadata with optimistic concurrency.</summary>
    [HttpPut("branches/{branchId:guid}", Name = "updateBranch")]
    public async Task<ActionResult<MutationResponse<BranchResponse>>> UpdateBranch(Guid schoolId, Guid branchId, UpdateBranchRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct) =>
     Ok(await school.SaveBranchAsync(schoolId, branchId, operation, new(request.Name, request.AddressLine, request.City, request.PostalCode, request.CountryCode), request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>List only groups visible through the caller's school relationships.</summary>
    [HttpGet("groups", Name = "listGroups")]
    public async Task<ActionResult<IReadOnlyList<GroupResponse>>> Groups(Guid schoolId, CancellationToken ct) => Ok(await school.GroupsAsync(schoolId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Read one authorized group.</summary>
    [HttpGet("groups/{groupId:guid}", Name = "getGroup")]
    public async Task<ActionResult<GroupResponse>> Group(Guid schoolId, Guid groupId, CancellationToken ct) => Ok(await school.GroupAsync(schoolId, groupId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Create an empty group and its initial immutable empty roster.</summary>
    [HttpPost("groups", Name = "createGroup")]
    public async Task<ActionResult<MutationResponse<GroupResponse>>> CreateGroup(Guid schoolId, CreateGroupRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        var result = await school.SaveGroupAsync(schoolId, null, operation, request, null, await actors.CreateAsync(HttpContext, ct), ct);
        return Mutation(result, $"/api/v1/schools/{schoolId:D}/groups/{result.Resource.GroupId:D}");
    }
    /// <summary>Rename a group; branch, category and periods remain outside this command.</summary>
    [HttpPatch("groups/{groupId:guid}/name", Name = "renameGroup")]
    public async Task<ActionResult<MutationResponse<GroupResponse>>> RenameGroup(Guid schoolId, Guid groupId, RenameRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        var actor = await actors.CreateAsync(HttpContext, ct);
        var group = await school.GroupAsync(schoolId, groupId, actor, ct);
        return Ok(await school.SaveGroupAsync(schoolId, groupId, operation, new(request.Name, group.BranchId, group.CategoryCode), request.ExpectedRevision, actor, ct));
    }
    /// <summary>Read school-local roles; global profiles do not imply these roles.</summary>
    [HttpGet("memberships", Name = "getMemberships")]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> Memberships(Guid schoolId, CancellationToken ct) => Ok(await school.MembershipsAsync(schoolId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Read a specific enrollment under self, school administrator or assigned-teacher authority.</summary>
    [HttpGet("enrollments/{enrollmentId:guid}", Name = "getEnrollment")]
    public async Task<ActionResult<EnrollmentResponse>> Enrollment(Guid schoolId, Guid enrollmentId, CancellationToken ct) => Ok(await school.EnrollmentAsync(schoolId, enrollmentId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Check actual missing school planning dependencies; absent providers never produce complete=true.</summary>
    [HttpGet("planning-inputs", Name = "getPlanningInputs")]
    public async Task<ActionResult<PlanningInputsResponse>> Planning(Guid schoolId, CancellationToken ct) => Ok(await school.PlanningAsync(schoolId, await actors.CreateAsync(HttpContext, ct), ct));
    /// <summary>Create enrollment. Unavailable until verified Scheduling activation is implemented.</summary>
    [HttpPost("enrollments", Name = "createEnrollment")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> CreateEnrollment(Guid schoolId, CreateEnrollmentRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        SchoolRules.Id(request.StudentId);
        SchoolRules.Text(request.LicenseCategory, 20);
        await school.RejectActivationAsync(schoolId, "NewEnrollment", schoolId, operation, null, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>Assign group/programme. Fails before effects while Scheduling/Learning integration is absent.</summary>
    [HttpPut("enrollments/{enrollmentId:guid}/group-assignment", Name = "assignEnrollmentGroup")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> AssignGroup(Guid schoolId, Guid enrollmentId, GroupAssignmentRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        SchoolRules.Id(request.GroupId);
        SchoolRules.Id(request.ProgramVersionId);
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "Enrollment", enrollmentId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>End enrollment. No lifecycle activation without Scheduling and completion evidence where required.</summary>
    [HttpPost("enrollments/{enrollmentId:guid}/termination", Name = "endEnrollment")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> EndEnrollment(Guid schoolId, Guid enrollmentId, StatusRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        if (request.Status is not ("Completed" or "Withdrawn" or "Transferred" or "Cancelled"))
            throw SchoolException.Invalid();
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "Enrollment", enrollmentId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>End membership. Requires coordinated source invalidation; currently unavailable.</summary>
    [HttpPost("memberships/{membershipId:guid}/termination", Name = "endMembership")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> EndMembership(Guid schoolId, Guid membershipId, AcceptInvitationRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "Membership", membershipId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>Assign a teacher to a group. Activation is unavailable without Scheduling coordination.</summary>
    [HttpPost("groups/{groupId:guid}/teacher-assignments", Name = "assignTeacher")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Teacher(Guid schoolId, Guid groupId, TeacherAssignmentRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        SchoolRules.Id(request.TeacherId);
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "Group", groupId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>Change school status. Requires coordinated eligibility invalidation; currently unavailable.</summary>
    [HttpPost("status", Name = "changeSchoolStatus")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Status(Guid schoolId, StatusRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        if (request.Status is not ("Active" or "Inactive"))
            throw SchoolException.Invalid();
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "School", schoolId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    /// <summary>Change branch status. Requires coordinated eligibility invalidation; currently unavailable.</summary>
    [HttpPost("branches/{branchId:guid}/status", Name = "changeBranchStatus")]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> BranchStatus(Guid schoolId, Guid branchId, StatusRequest request, [FromHeader(Name = "Idempotency-Key")] Guid operation, CancellationToken ct)
    {
        if (request.Status is not ("Active" or "Inactive"))
            throw SchoolException.Invalid();
        SchoolRules.Revision(request.ExpectedRevision);
        await school.RejectActivationAsync(schoolId, "Branch", branchId, operation, request.ExpectedRevision, await actors.CreateAsync(HttpContext, ct), ct);
        return NoContent();
    }
    private ActionResult<MutationResponse<T>> Mutation<T>(MutationResponse<T> result, string location)
    {
        if (result.Replayed)
            return Ok(result);
        return Created(location, result);
    }
}
