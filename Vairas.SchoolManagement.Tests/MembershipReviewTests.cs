using System.Text.Json;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class MembershipReviewTests : BusinessFixture
{
    private UserDeactivatedEvent Event(Guid user, Guid? id = null, long revision = 2, char fingerprint = 'a') =>
        new(id ?? Guid.NewGuid(), user, revision, Clock.Now, new string(fingerprint, 64));

    [Test]
    public async Task Deactivated_member_raises_reviews_for_effective_memberships_and_active_enrollments_only()
    {
        var teacherMembership = Member(Target, SchoolRole.Teacher);
        var ended = Member(Target, SchoolRole.Student); ended.EndedAt = Clock.Now.AddHours(-1);
        var enrollment = Enroll();
        Activity.Profiles = new(Target, "Inactive", 2, Student, Teacher, null);
        var message = Event(Target);
        var outcome = await Service.ConsumeUserDeactivatedAsync(message, default);
        var reviews = Store.Rows.OfType<MembershipReview>().OrderBy(x => x.TargetKind).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(new DeactivationOutcome("ReviewsRaised", 2)));
            Assert.That(reviews.Select(x => (x.TargetKind, x.TargetId, x.SubjectRole)), Is.EqualTo(new[] { ("Enrollment", enrollment.EnrollmentId, "Student"), ("Membership", teacherMembership.MembershipId, "Teacher") }));
            Assert.That(reviews.All(x => x.ReasonCode == SchoolService.DeactivationReason && x.SubjectUserId == Target && x.RaisedAt == Clock.Now), Is.True);
            Assert.That(Store.Rows.OfType<SchoolMembership>().Single(x => x.MembershipId == teacherMembership.MembershipId).EndedAt, Is.Null);
            Assert.That(Store.Rows.OfType<StudentEnrollment>().Single().Status, Is.EqualTo(EnrollmentStatus.Active));
        });
        Assert.That(await Service.ConsumeUserDeactivatedAsync(message, default), Is.EqualTo(outcome));
        Assert.That(Store.Rows.OfType<MembershipReview>().Count(), Is.EqualTo(2));
        Error(async () => await Service.ConsumeUserDeactivatedAsync(message with { Fingerprint = new string('b', 64) }, default), "EVENT_ID_REUSED");
    }

    [Test]
    public async Task Student_membership_and_enrollment_sharing_one_identifier_raise_two_distinct_reviews()
    {
        // School issues a student's membership with the enrollment's identifier; review identity must include the target kind.
        var enrollment = Enroll();
        var membership = Member(Target, SchoolRole.Student); membership.MembershipId = enrollment.EnrollmentId;
        Activity.Profiles = new(Target, "Inactive", 2, Student, null, null);
        var message = Event(Target);
        Assert.That(await Service.ConsumeUserDeactivatedAsync(message, default), Is.EqualTo(new DeactivationOutcome("ReviewsRaised", 2)));
        Assert.That(Store.Rows.OfType<MembershipReview>().Select(x => (x.TargetKind, x.TargetId)).Order(),
            Is.EqualTo(new[] { ("Enrollment", enrollment.EnrollmentId), ("Membership", enrollment.EnrollmentId) }));
        Store.Rows.RemoveAll(x => x is SchoolIntegrationInbox);
        Assert.That((await Service.ConsumeUserDeactivatedAsync(message, default)).ReviewsRaised, Is.Zero);
        Assert.That(Store.Rows.OfType<MembershipReview>().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Active_users_users_without_relationships_and_stale_owner_evidence_are_handled_explicitly()
    {
        Activity.Profiles = new(Target, "Active", 2, null, null, null);
        Assert.That((await Service.ConsumeUserDeactivatedAsync(Event(Target), default)).Outcome, Is.EqualTo("UserActive"));
        Activity.Profiles = new(Target, "Inactive", 2, null, null, null);
        Assert.That((await Service.ConsumeUserDeactivatedAsync(Event(Target), default)).Outcome, Is.EqualTo("NoActiveRelationships"));
        Error(async () => await Service.ConsumeUserDeactivatedAsync(Event(Target, revision: 3), default), "DEPENDENCY_UNAVAILABLE");
        Error(async () => await Service.ConsumeUserDeactivatedAsync(Event(Target) with { Fingerprint = "x" }, default), "VALIDATION_FAILED");
        Assert.That(Store.Rows.OfType<MembershipReview>(), Is.Empty);
    }

    [Test]
    public async Task Administrators_list_and_resolve_reviews_once_and_others_cannot()
    {
        Member(Target, SchoolRole.Teacher);
        Activity.Profiles = new(Target, "Inactive", 2, null, Teacher, null);
        await Service.ConsumeUserDeactivatedAsync(Event(Target), default);
        var open = await Service.MembershipReviewsAsync(School, false, Admin, default);
        Assert.That(open, Has.Count.EqualTo(1));
        var resolved = await Service.ResolveMembershipReviewAsync(School, open[0].ReviewId, new(" Membership ended after review "), Admin, default);
        Assert.Multiple(() =>
        {
            Assert.That(resolved.ResolutionNote, Is.EqualTo("Membership ended after review"));
            Assert.That(resolved.ResolvedByUserId, Is.EqualTo(AdminUser));
            Assert.That(resolved.ResolvedAt, Is.EqualTo(Clock.Now));
        });
        Assert.That(await Service.ResolveMembershipReviewAsync(School, open[0].ReviewId, new("Membership ended after review"), Admin, default), Is.EqualTo(resolved));
        Error(async () => await Service.ResolveMembershipReviewAsync(School, open[0].ReviewId, new("Different"), Admin, default), "REVIEW_ALREADY_RESOLVED");
        Error(async () => await Service.ResolveMembershipReviewAsync(School, open[0].ReviewId, new(" "), Admin, default), "VALIDATION_FAILED");
        Error(async () => await Service.ResolveMembershipReviewAsync(School, Guid.NewGuid(), new("x"), Admin, default), "NOT_FOUND");
        Assert.That(await Service.MembershipReviewsAsync(School, false, Admin, default), Is.Empty);
        Assert.That(await Service.MembershipReviewsAsync(School, true, Admin, default), Has.Count.EqualTo(1));
        var teacher = Other; Member(teacher, SchoolRole.Teacher);
        Error(async () => await Service.MembershipReviewsAsync(School, false, Actor(teacher), default), "NOT_FOUND");
    }

    private static (string Id, BinaryData Body) Envelope(Action<Dictionary<string, object?>, Dictionary<string, object?>>? change = null)
    {
        var id = Guid.NewGuid(); var user = Guid.NewGuid();
        var headers = new Dictionary<string, object?> { ["eventId"] = id, ["eventType"] = "UserDeactivated", ["eventVersion"] = 1, ["occurredAt"] = "2026-11-23T11:59:00Z",
            ["producer"] = "user", ["aggregateId"] = user, ["aggregateVersion"] = 2 };
        var payload = new Dictionary<string, object?> { ["userId"] = user, ["deactivatedAt"] = "2026-11-23T11:59:00Z" };
        change?.Invoke(headers, payload);
        return (id.ToString("D"), BinaryData.FromString(JsonSerializer.Serialize(new { headers, payload })));
    }

    [Test]
    public void Decode_accepts_only_a_consistent_user_management_envelope()
    {
        var (id, body) = Envelope();
        var message = SchoolInboxWorker.Decode(id, "UserDeactivated", body);
        Assert.That((message.EventId.ToString("D"), message.UserRevision), Is.EqualTo((id, 2L)));
        Assert.That(message.Fingerprint, Does.Match("^[0-9a-f]{64}$"));
        Error(() => SchoolInboxWorker.Decode(Guid.NewGuid().ToString("D"), "UserDeactivated", body), "VALIDATION_FAILED");
        Error(() => SchoolInboxWorker.Decode(id, "UserCreated", body), "VALIDATION_FAILED");
        foreach (var change in new Action<Dictionary<string, object?>, Dictionary<string, object?>>[]
                 { (h, _) => h["producer"] = "school", (h, _) => h["eventVersion"] = 2, (h, _) => h["aggregateId"] = Guid.NewGuid(), (h, _) => h["aggregateVersion"] = 0, (_, p) => p.Remove("userId") })
        {
            var (bad, badBody) = Envelope(change);
            Error(() => SchoolInboxWorker.Decode(bad, "UserDeactivated", badBody), "VALIDATION_FAILED");
        }
        Error(() => SchoolInboxWorker.Decode(id, "UserDeactivated", BinaryData.FromString("{")), "VALIDATION_FAILED");
        Error(() => SchoolInboxWorker.Decode(id, "UserDeactivated", BinaryData.FromString(new string(' ', 16385))), "VALIDATION_FAILED");
    }
}
