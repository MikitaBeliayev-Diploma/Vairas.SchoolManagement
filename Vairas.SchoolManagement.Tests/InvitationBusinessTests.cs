using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class InvitationBusinessTests : BusinessFixture
{
    [TestCase(SchoolRole.Student)]
    [TestCase(SchoolRole.Teacher)]
    public async Task Issue_exact_target_24h_and_retry_no_duplicate(SchoolRole role)
    {
        var op = Guid.NewGuid();
        var first = await Issue(role, op);
        var repeat = await Issue(role, op);
        Assert.That(first.Resource.TargetUserId, Is.EqualTo(Target));
        Assert.That(first.Resource.ExpiresAt - first.Resource.CreatedAt, Is.EqualTo(TimeSpan.FromHours(24)));
        Assert.That(repeat.Resource.InvitationId, Is.EqualTo(first.Resource.InvitationId));
        Assert.That(repeat.Replayed, Is.True);
        Assert.That(Store.Rows.OfType<StudentEnrollment>(), Is.Empty);
        Assert.That((await Service.InvitationAsync(School, first.Resource.InvitationId, Actor(Target), default)).Status, Is.EqualTo(InvitationStatus.Pending));
        Error(async () => await Service.InvitationAsync(School, first.Resource.InvitationId, Actor(Other), default), "NOT_FOUND");
    }
    [Test]
    public async Task Administrator_issue_is_operator_only()
    {
        Error(async () => await Issue(SchoolRole.Administrator), "ACCESS_DENIED");
        Access.PlatformOperatorUserIds = [AdminUser];
        var r = await Issue(SchoolRole.Administrator);
        Assert.That(Store.Rows.OfType<SchoolInvitation>().Single().IssuerMembershipId, Is.Null);
        Assert.That(r.Resource.Role, Is.EqualTo(SchoolRole.Administrator));
    }
    [Test]
    public async Task Own_invitation_paging_never_returns_foreign_target()
    {
        for (var i = 0; i < 3; i++)
            await Issue();
        Target = Other;
        await Issue();
        var own = Store.Rows.OfType<SchoolInvitation>().First().TargetUserId;
        var page = await Service.MyInvitationsAsync(null, 2, null, Actor(own), default);
        Assert.That(page.Items.Count, Is.EqualTo(2));
        Assert.That(page.NextCursor, Is.Not.Null);
        var next = await Service.MyInvitationsAsync(School, 2, page.NextCursor, Actor(own), default);
        Assert.That(next.Items.Count, Is.EqualTo(1));
        Assert.That(next.NextCursor, Is.Null);
        Assert.That(next.Items.All(x => x.TargetUserId == own), Is.True);
        Error(async () => await Service.MyInvitationsAsync(null, 0, null, Actor(own), default), "VALIDATION_FAILED");
        Error(async () => await Service.MyInvitationsAsync(null, 20, "invalid", Actor(own), default), "VALIDATION_FAILED");
    }
    [Test]
    public async Task Revoke_is_local_atomic_replayable_and_terminal()
    {
        var issued = await Issue();
        var id = issued.Resource.InvitationId;
        var op = Guid.NewGuid();
        var request = new RevokeInvitationRequest(1, " withdrawn ");
        var result = await Service.RevokeAsync(School, id, op, request, Admin, default);
        Assert.That(result.Resource.Status, Is.EqualTo(InvitationStatus.Revoked));
        Assert.That((await Service.RevokeAsync(School, id, op, request, Admin, default)).Replayed, Is.True);
        Error(async () => await Service.RevokeAsync(School, id, Guid.NewGuid(), new(2, "again"), Admin, default), "INVITATION_TERMINAL");
        Error(async () => await Service.RevokeAsync(School, id, op, new(1, "changed"), Admin, default), "OPERATION_KEY_REUSE");
        Error(async () => await Service.RevokeAsync(School, id, Guid.NewGuid(), new(2, "foreign"), Actor(Other), default), "NOT_FOUND");
    }
    [Test]
    public async Task Expired_invitation_is_visible_as_expired_and_can_be_revoked()
    {
        var i = await Issue();
        Clock.Now = Clock.Now.AddDays(2);
        Assert.That((await Service.InvitationAsync(School, i.Resource.InvitationId, Actor(Target), default)).EffectiveStatus, Is.EqualTo("Expired"));
        Error(async () => await Service.AcceptAsync(School, i.Resource.InvitationId, Guid.NewGuid(), new(1), Actor(Target), default), "INVITATION_EXPIRED");
        Assert.That((await Service.RevokeAsync(School, i.Resource.InvitationId, Guid.NewGuid(), new(1, "expired cleanup"), Admin, default)).Resource.Status, Is.EqualTo(InvitationStatus.Revoked));
    }
    [TestCase(SchoolRole.Student)]
    [TestCase(SchoolRole.Teacher)]
    public async Task New_membership_never_activates_without_gate(SchoolRole role)
    {
        var i = await Issue(role);
        var count = Store.Rows.Count;
        Error(async () => await Service.AcceptAsync(School, i.Resource.InvitationId, Guid.NewGuid(), new(1), Actor(Target), default), "ACTIVATION_INTEGRATION_NOT_READY");
        Assert.That(Store.Rows.Count, Is.EqualTo(count));
        Assert.That(Store.Rows.OfType<SchoolInvitation>().Single().Status, Is.EqualTo(InvitationStatus.Pending));
    }
    [Test]
    public async Task Staff_profile_prerequisite_is_not_school_membership()
    {
        var i = await Issue();
        Error(async () => await Service.AcceptAsync(School, i.Resource.InvitationId, Guid.NewGuid(), new(1), Actor(Target) with { TeacherId = null }, default), "PROFILE_REQUIRED");
        Error(async () => await Service.AcceptAsync(School, i.Resource.InvitationId, Guid.NewGuid(), new(1), Actor(Other), default), "NOT_FOUND");
    }
    [Test]
    public async Task Unchanged_staff_reuse_accepts_once_and_replays_after_expiry()
    {
        Member(Target, SchoolRole.Teacher);
        var i = await Issue();
        var id = i.Resource.InvitationId;
        var op = Guid.NewGuid();
        var first = await Service.AcceptAsync(School, id, op, new(1), Actor(Target), default);
        Assert.That(first.Outcome, Is.EqualTo("Completed"));
        Assert.That(first.ResultEnrollmentId, Is.Null);
        Assert.That((await Service.AcceptAsync(School, id, op, new(1), Actor(Target), default)).OperationId, Is.EqualTo(op));
        Error(async () => await Service.AcceptAsync(School, id, op, new(2), Actor(Target), default), "OPERATION_KEY_REUSE");
        Clock.Now = Clock.Now.AddDays(2);
        Assert.That((await Service.AcceptAsync(School, id, Guid.NewGuid(), new(1), Actor(Target), default)).OperationId, Is.EqualTo(op));
        Assert.That(Store.Rows.OfType<SchoolMembership>().Count(x => x.UserId == Target), Is.EqualTo(1));
        Assert.That((await Service.InvitationAsync(School, id, Actor(Target), default)).AcceptanceState, Is.EqualTo("Completed"));
        Error(async () => await Service.RevokeAsync(School, id, Guid.NewGuid(), new(2, "no"), Admin, default), "INVITATION_TERMINAL");
    }
    [Test]
    public async Task First_accept_checks_current_revision_issuer_and_activity()
    {
        var i = await Issue();
        var id = i.Resource.InvitationId;
        Error(async () => await Service.AcceptAsync(School, id, Guid.NewGuid(), new(2), Actor(Target), default), "REVISION_CONFLICT");
        Activity.Inactive.Add(AdminUser);
        Error(async () => await Service.AcceptAsync(School, id, Guid.NewGuid(), new(1), Actor(Target), default), "ACCESS_DENIED");
        Activity.Inactive.Clear();
        Store.Rows.OfType<SchoolMembership>().Single().EndedAt = Clock.Now;
        Error(async () => await Service.AcceptAsync(School, id, Guid.NewGuid(), new(1), Actor(Target), default), "ACCESS_DENIED");
    }
    [Test]
    public async Task Provisioning_is_exact_local_read_only_and_echoes_bindings()
    {
        var i = await Issue();
        var r = new SchoolAuthorityRequest(Target, Target, School, i.Resource.InvitationId, SchoolRole.Teacher, false);
        var result = await Service.ProvisioningAsync(r, default);
        Assert.That(result.Allowed, Is.True);
        Assert.That(result.InvitationId, Is.EqualTo(r.InvitationId));
        Assert.That(result.ValidUntil, Is.GreaterThan(Clock.Now));
        Assert.That(Activity.Calls, Is.Empty);
        Assert.That((await Service.ProvisioningAsync(r with
        {
            TargetUserId = Other
        }, default)).Allowed, Is.False);
        Assert.That((await Service.ProvisioningAsync(r with
        {
            Role = SchoolRole.Administrator
        }, default)).Allowed, Is.False);
        Clock.Now = Clock.Now.AddDays(2);
        Assert.That((await Service.ProvisioningAsync(r, default)).Allowed, Is.False);
    }
    [Test]
    public async Task Bootstrap_requires_operator_existing_active_school_and_no_effective_admin()
    {
        Access.PlatformOperatorUserIds = [Other];
        var r = new SchoolAuthorityRequest(Other, Target, School, null, SchoolRole.Administrator, true);
        Assert.That((await Service.ProvisioningAsync(r, default)).Allowed, Is.False);
        Store.Rows.OfType<SchoolMembership>().Single().EndedAt = Clock.Now;
        Assert.That((await Service.ProvisioningAsync(r, default)).Allowed, Is.True);
        Assert.That((await Service.ProvisioningAsync(r with
        {
            ActorUserId = Target
        }, default)).Allowed, Is.False);
        Assert.That((await Service.ProvisioningAsync(r with
        {
            SchoolId = Guid.NewGuid()
        }, default)).Allowed, Is.False);
        Assert.That(Store.Commits, Is.Zero);
    }
}
