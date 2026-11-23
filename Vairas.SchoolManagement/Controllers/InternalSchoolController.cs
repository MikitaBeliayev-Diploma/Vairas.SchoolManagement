using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Controllers;

[ApiController, Route("internal/v1")]
public sealed class InternalSchoolController(ISchoolService school) : ControllerBase
{
    /// <summary>Local-only read-only invitation/bootstrap validation for User Management; never calls UM back.</summary>
    [HttpPost("profile-provisioning/validate", Name = "validateProfileProvisioning"), Authorize(Policy = "school.profile-provisioning")]
    public async Task<ActionResult<SchoolAuthorityResponse>> Provisioning(SchoolAuthorityRequest request, CancellationToken ct) => Ok(await school.ProvisioningAsync(request, ct));
    /// <summary>Validate current school/enrollment/actor/teacher scope using authoritative global bindings.</summary>
    [HttpGet("enrollments/{enrollmentId:guid}/eligibility", Name = "checkEnrollmentEligibility"), Authorize(Policy = "school.eligibility.read")]
    public async Task<ActionResult<EligibilityResponse>> Eligibility(Guid enrollmentId, Guid schoolId, Guid actingUserId, Guid teacherId, CancellationToken ct) => Ok(await school.EligibilityAsync(schoolId, enrollmentId, actingUserId, teacherId, ct));
    /// <summary>Return the exact retained roster revision for a full lesson interval; never substitute current roster.</summary>
    [HttpGet("groups/{groupId:guid}/participants", Name = "getGroupParticipants"), Authorize(Policy = "school.participants.read")]
    public async Task<ActionResult<ParticipantsResponse>> Participants(Guid groupId, Guid schoolId, long? membershipRevision, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => Ok(await school.ParticipantsAsync(schoolId, groupId, membershipRevision, from, to, ct));
}
