using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>
/// UserDeactivated is not coordinated with School. Admission already re-checks User Management status on every command,
/// so a deactivated user cannot act; this consumer surfaces the user's still-effective memberships and active
/// enrollments to each school's administrators without changing them.
/// </summary>
public sealed partial class SchoolService : IMembershipReviewService
{
    public const string DeactivationReason = "MemberDeactivated";

    public async Task<DeactivationOutcome> ConsumeUserDeactivatedAsync(UserDeactivatedEvent message, CancellationToken ct)
    {
        if (message.EventId == Guid.Empty || message.UserId == Guid.Empty || message.UserRevision < 1 || message.Fingerprint is not { Length: 64 }) throw SchoolErrors.Invalid();
        await using (var read = await store.OpenAsync(Guid.Empty, false, ct))
        {
            var prior = await read.FindAsync<SchoolIntegrationInbox>(x => x.EventId == message.EventId, ct);
            if (prior is not null) return prior.Fingerprint == message.Fingerprint ? new(prior.Outcome, prior.ReviewsRaised) : throw SchoolErrors.Conflict("EVENT_ID_REUSED");
        }
        var user = await activity.RoleProfilesAsync(message.UserId, ct);
        if (user.UserId != message.UserId || user.Revision < message.UserRevision) throw SchoolErrors.Dependency();
        var raised = 0; string outcome;
        if (user.Status != "Inactive") outcome = "UserActive";
        else
        {
            foreach (var school in await store.DiscoverAsync(message.UserId, user.StudentId, ct))
            {
                await using var s = await store.OpenAsync(school, true, ct);
                var now = await s.NowAsync(ct);
                var existing = (await s.ListAsync<MembershipReview>(x => x.SourceEventId == message.EventId, ct)).Select(x => (x.TargetKind, x.TargetId)).ToHashSet();
                var targets = new List<(string Kind, Guid Id, string Role)>();
                foreach (var m in await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == message.UserId, ct))
                    if (m.EndedAt is null || m.EndedAt > now) targets.Add(("Membership", m.MembershipId, m.Role.ToString()));
                if (user.StudentId is Guid student)
                    foreach (var e in await s.ListAsync<StudentEnrollment>(x => x.SchoolId == school && x.StudentId == student && x.Status == EnrollmentStatus.Active, ct))
                        targets.Add(("Enrollment", e.EnrollmentId, "Student"));
                foreach (var (kind, id, role) in targets.Where(x => !existing.Contains((x.Kind, x.Id))))
                {
                    s.Add(new MembershipReview { ReviewId = Guid.NewGuid(), SchoolId = school, TargetKind = kind, TargetId = id, ReasonCode = DeactivationReason,
                        SubjectRole = role, SubjectUserId = message.UserId, SourceEventId = message.EventId, RaisedAt = now });
                    raised++;
                }
                await s.CommitAsync(ct);
            }
            outcome = raised > 0 ? "ReviewsRaised" : "NoActiveRelationships";
        }
        await using var write = await store.OpenAsync(Guid.Empty, true, ct);
        var concurrent = await write.FindAsync<SchoolIntegrationInbox>(x => x.EventId == message.EventId, ct);
        if (concurrent is not null) return new(concurrent.Outcome, concurrent.ReviewsRaised);
        write.Add(new SchoolIntegrationInbox { EventId = message.EventId, Producer = "user", EventType = "UserDeactivated", Fingerprint = message.Fingerprint,
            Outcome = outcome, ReviewsRaised = raised, ProcessedAt = await write.NowAsync(ct) });
        await write.CommitAsync(ct);
        return new(outcome, raised);
    }

    public async Task<IReadOnlyList<MembershipReviewResponse>> MembershipReviewsAsync(Guid school, bool includeResolved, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, true, false, ct);
        return (await s.ListAsync<MembershipReview>(x => x.SchoolId == school && (includeResolved || x.ResolvedAt == null), ct))
            .OrderBy(x => x.RaisedAt).ThenBy(x => x.ReviewId).Select(ReviewDto).ToArray();
    }

    public async Task<MembershipReviewResponse> ResolveMembershipReviewAsync(Guid school, Guid review, ResolveMembershipReviewRequest request, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(review);
        var note = request.Note?.Trim() ?? "";
        if (note.Length is < 1 or > 500) throw SchoolErrors.Invalid();
        await using var s = await Open(school, actor, true, true, ct);
        var row = await s.FindAsync<MembershipReview>(x => x.ReviewId == review && x.SchoolId == school, ct) ?? throw SchoolErrors.Missing();
        if (row.ResolvedAt is not null)
            return row.ResolvedByUserId == actor.UserId && row.ResolutionNote == note ? ReviewDto(row) : throw SchoolErrors.Conflict("REVIEW_ALREADY_RESOLVED");
        row.ResolvedAt = await s.NowAsync(ct); row.ResolvedByUserId = actor.UserId; row.ResolutionNote = note;
        await s.CommitAsync(ct);
        return ReviewDto(row);
    }

    private static MembershipReviewResponse ReviewDto(MembershipReview x) => new(x.ReviewId, x.SchoolId, x.TargetKind, x.TargetId, x.ReasonCode, x.SubjectRole,
        x.SubjectUserId, x.SourceEventId, x.RaisedAt, x.ResolvedAt, x.ResolvedByUserId, x.ResolutionNote);
}
