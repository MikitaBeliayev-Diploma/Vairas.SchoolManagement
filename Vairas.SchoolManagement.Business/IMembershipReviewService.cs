using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface IMembershipReviewService
{
    Task<DeactivationOutcome> ConsumeUserDeactivatedAsync(UserDeactivatedEvent message, CancellationToken ct);
    Task<IReadOnlyList<MembershipReviewResponse>> MembershipReviewsAsync(Guid school, bool includeResolved, SchoolActor actor, CancellationToken ct);
    Task<MembershipReviewResponse> ResolveMembershipReviewAsync(Guid school, Guid review, ResolveMembershipReviewRequest request, SchoolActor actor, CancellationToken ct);
}
