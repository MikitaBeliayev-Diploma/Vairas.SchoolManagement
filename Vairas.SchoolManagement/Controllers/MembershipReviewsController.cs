using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Controllers;

[Authorize(Policy = "SchoolEntry"), Route("api/v1/schools/{schoolId:guid}/membership-reviews")]
public sealed class MembershipReviewsController(IMembershipReviewService reviews, ISchoolActorFactory actors) : BaseApiController
{
    /// <summary>List memberships and enrollments an administrator must review because the user's account was deactivated.</summary>
    [HttpGet(Name = "listMembershipReviews")]
    public async Task<ActionResult<IReadOnlyList<MembershipReviewResponse>>> List(Guid schoolId, bool includeResolved, CancellationToken ct) =>
        Ok(await reviews.MembershipReviewsAsync(schoolId, includeResolved, await actors.CreateAsync(HttpContext, ct), ct));

    /// <summary>Record the administrator's decision once; end the membership or enrollment through its own coordinated command first.</summary>
    [HttpPost("{reviewId:guid}/resolution", Name = "resolveMembershipReview")]
    public async Task<ActionResult<MembershipReviewResponse>> Resolve(Guid schoolId, Guid reviewId, ResolveMembershipReviewRequest request, CancellationToken ct) =>
        Ok(await reviews.ResolveMembershipReviewAsync(schoolId, reviewId, request, await actors.CreateAsync(HttpContext, ct), ct));
}
