using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class SchoolBusinessTests : BusinessFixture
{
    [Test]
    public async Task School_authority_is_local_and_foreign_school_is_not_visible()
    {
        Assert.That((await Service.GetSchoolAsync(School, Admin, default)).Name, Is.EqualTo("School"));
        Error(async () => await Service.GetSchoolAsync(Guid.NewGuid(), Admin, default), "NOT_FOUND");
        Error(async () => await Service.GetSchoolAsync(School, Actor(Target), default), "NOT_FOUND");
        Access.PlatformOperatorUserIds = [Target];
        Assert.That((await Service.GetSchoolAsync(School, Actor(Target), default)).SchoolId, Is.EqualTo(School));
    }
    [Test]
    public void Global_profile_and_ended_membership_grant_nothing()
    {
        var m = Member(Target, SchoolRole.Teacher);
        m.EndedAt = Clock.Now;
        Error(async () => await Service.GetSchoolAsync(School, Actor(Target), default), "NOT_FOUND");
    }
    [Test]
    public void Inactive_school_and_expired_admission_deny()
    {
        Store.Rows.OfType<School>().Single().Status = SchoolStatus.Inactive;
        Error(async () => await Service.GetSchoolAsync(School, Admin, default), "ACCESS_DENIED");
        Error(async () => await Service.GetSchoolAsync(School, Admin with { ValidUntil = Clock.Now }, default), "DEPENDENCY_UNAVAILABLE");
    }
    [Test]
    public void Lock_wait_cannot_extend_admission()
    {
        var actor = Admin;
        Store.OnOpen = () => Clock.Now = Clock.Now.AddSeconds(31);
        Error(async () => await Service.GetSchoolAsync(School, actor, default), "DEPENDENCY_UNAVAILABLE");
    }
    [Test]
    public async Task Rename_checks_revision_replays_and_conflicts()
    {
        var op = Guid.NewGuid();
        var r = new RenameRequest(1, " New school ");
        Assert.That((await Service.RenameSchoolAsync(School, op, r, Admin, default)).Resource.Revision, Is.EqualTo(2));
        Assert.That((await Service.RenameSchoolAsync(School, op, r, Admin, default)).Replayed, Is.True);
        Error(async () => await Service.RenameSchoolAsync(School, op, new(1, "Changed"), Admin, default), "OPERATION_KEY_REUSE");
        Error(async () => await Service.RenameSchoolAsync(School, Guid.NewGuid(), r, Admin, default), "REVISION_CONFLICT");
    }
    [Test]
    public async Task Branch_lifecycle_normalization_and_replay()
    {
        var op = Guid.NewGuid();
        var request = new CreateBranchRequest(" Branch ", " Address ", " City ", " Code ", "lt");
        var created = await Service.SaveBranchAsync(School, null, op, request, null, Admin, default);
        Assert.That(created.Resource.CountryCode, Is.EqualTo("LT"));
        Assert.That((await Service.SaveBranchAsync(School, null, op, request, null, Admin, default)).Resource.BranchId, Is.EqualTo(created.Resource.BranchId));
        Assert.That((await Service.BranchesAsync(School, Admin, default)).Count, Is.EqualTo(1));
        Assert.That((await Service.BranchAsync(School, created.Resource.BranchId, Admin, default)).Name, Is.EqualTo("Branch"));
        var update = Guid.NewGuid();
        var next = request with
        {
            Name = "Renamed"
        };
        Assert.That((await Service.SaveBranchAsync(School, created.Resource.BranchId, update, next, 1, Admin, default)).Resource.Revision, Is.EqualTo(2));
        Assert.That((await Service.SaveBranchAsync(School, created.Resource.BranchId, update, next, 1, Admin, default)).Replayed, Is.True);
        Error(async () => await Service.SaveBranchAsync(School, created.Resource.BranchId, Guid.NewGuid(), next, 1, Admin, default), "REVISION_CONFLICT");
        Error(async () => await Service.BranchAsync(School, Guid.NewGuid(), Admin, default), "NOT_FOUND");
        Error(async () => await Service.SaveBranchAsync(School, Guid.NewGuid(), Guid.NewGuid(), next, 1, Admin, default), "NOT_FOUND");
    }
    [TestCase("L")]
    [TestCase("123")]
    [TestCase("11")]
    public void Invalid_country_never_commits(string country)
    {
        Error(async () => await Service.SaveBranchAsync(School, null, Guid.NewGuid(), new("Branch", null, null, null, country), null, Admin, default), "VALIDATION_FAILED");
        Assert.That(Store.Commits, Is.Zero);
    }
    [Test]
    public async Task Empty_group_has_retained_empty_basis_and_name_updates_only()
    {
        var op = Guid.NewGuid();
        var request = new CreateGroupRequest("Group");
        var created = await Service.SaveGroupAsync(School, null, op, request, null, Admin, default);
        var id = created.Resource.GroupId;
        Assert.That(Store.Rows.OfType<GroupRosterRevision>().Single().MembershipRevision, Is.EqualTo(1));
        Assert.That((await Service.SaveGroupAsync(School, null, op, request, null, Admin, default)).Resource.GroupId, Is.EqualTo(id));
        Assert.That((await Service.GroupsAsync(School, Admin, default)).Count, Is.EqualTo(1));
        Assert.That((await Service.GroupAsync(School, id, Admin, default)).Name, Is.EqualTo("Group"));
        var update = Guid.NewGuid();
        var renamed = new CreateGroupRequest("Renamed");
        Assert.That((await Service.SaveGroupAsync(School, id, update, renamed, 1, Admin, default)).Resource.Revision, Is.EqualTo(2));
        Assert.That((await Service.SaveGroupAsync(School, id, update, renamed, 1, Admin, default)).Replayed, Is.True);
        Error(async () => await Service.SaveGroupAsync(School, id, Guid.NewGuid(), renamed, 1, Admin, default), "REVISION_CONFLICT");
        Error(async () => await Service.SaveGroupAsync(School, id, Guid.NewGuid(), renamed with { CategoryCode = "B" }, 2, Admin, default), "ACTIVATION_INTEGRATION_NOT_READY");
    }
    [Test]
    public void Group_rejects_foreign_branch_and_missing_resources()
    {
        Error(async () => await Service.SaveGroupAsync(School, null, Guid.NewGuid(), new("Group", Guid.NewGuid()), null, Admin, default), "NOT_FOUND");
        Error(async () => await Service.SaveGroupAsync(School, Guid.NewGuid(), Guid.NewGuid(), new("Group"), 1, Admin, default), "NOT_FOUND");
        Error(async () => await Service.GroupAsync(School, Guid.NewGuid(), Admin, default), "NOT_FOUND");
    }
    [Test]
    public async Task Teacher_and_student_read_only_their_assigned_groups()
    {
        TrainingGroup();
        Enroll();
        Member(Target, SchoolRole.Teacher);
        Member(Other, SchoolRole.Student);
        Assert.That(await Service.GroupsAsync(School, Actor(Target), default), Is.Empty);
        Error(async () => await Service.GroupAsync(School, Group, Actor(Target), default), "NOT_FOUND");
        Store.Rows.Add(new TeacherGroupAssignment { SchoolId = School, GroupId = Group, TeacherId = Teacher, AssignedAt = Clock.Now.AddDays(-1) });
        Assert.That((await Service.GroupsAsync(School, Actor(Target), default)).Count, Is.EqualTo(1));
        Store.Rows.Add(new StudentGroupMembership { SchoolId = School, GroupId = Group, EnrollmentId = Enrollment, JoinedAt = Clock.Now.AddDays(-1) });
        Assert.That((await Service.GroupsAsync(School, Actor(Other) with
        {
            TeacherId = null
        }, default)).Count, Is.EqualTo(1));
    }
    [Test]
    public async Task Enrollment_reads_keep_identity_and_tenant_boundaries()
    {
        Enroll();
        Member(Target, SchoolRole.Student);
        Member(Other, SchoolRole.Teacher);
        TrainingGroup();
        Assert.That((await Service.EnrollmentAsync(School, Enrollment, Actor(Target), default)).StudentId, Is.EqualTo(Student));
        Assert.That((await Service.EnrollmentAsync(School, Enrollment, Admin, default)).EnrollmentId, Is.EqualTo(Enrollment));
        var teacher = Actor(Other) with
        {
            StudentId = null
        };
        Error(async () => await Service.EnrollmentAsync(School, Enrollment, teacher, default), "NOT_FOUND");
        Store.Rows.Add(new TeacherGroupAssignment { SchoolId = School, GroupId = Group, TeacherId = Teacher, AssignedAt = Clock.Now.AddDays(-1) });
        Store.Rows.Add(new StudentGroupMembership { SchoolId = School, GroupId = Group, EnrollmentId = Enrollment, JoinedAt = Clock.Now.AddDays(-1) });
        Assert.That((await Service.EnrollmentAsync(School, Enrollment, teacher, default)).Version, Is.EqualTo(1));
        Assert.That((await Service.MyEnrollmentsAsync(Actor(Target), default)).Count, Is.EqualTo(1));
        Assert.That(await Service.MyEnrollmentsAsync(Actor(Target) with
        {
            StudentId = null
        }, default), Is.Empty);
        Error(async () => await Service.EnrollmentAsync(School, Guid.NewGuid(), Admin, default), "NOT_FOUND");
    }
    [Test]
    public async Task Membership_directory_is_admin_only_and_planning_is_not_faked()
    {
        Member(Target, SchoolRole.Student);
        Assert.That((await Service.MembershipsAsync(School, Admin, default)).Count, Is.EqualTo(2));
        Error(async () => await Service.MembershipsAsync(School, Actor(Target), default), "NOT_FOUND");
        var p = await Service.PlanningAsync(School, Admin, default);
        Assert.That(p.Complete, Is.False);
        Assert.That(p.MissingItems, Does.Contain("TrainingGroups"));
        Enroll();
        TrainingGroup();
        p = await Service.PlanningAsync(School, Admin, default);
        Assert.That(p.MissingItems, Does.Not.Contain("ActiveEnrollments"));
        // Owner-specific checks are evaluated for the selected draft period at submission, so the preliminary read names them as open.
        Assert.That(p.MissingItems, Does.Contain("DraftSpecificSchedulingValidation").And.Contain("LearningRequirementValidation"));
    }
    [TestCase("School")]
    [TestCase("Branch")]
    [TestCase("Group")]
    [TestCase("Enrollment")]
    [TestCase("Membership")]
    [TestCase("NewEnrollment")]
    public void Missing_gate_changes_no_business_rows(string kind)
    {
        Enroll();
        TrainingGroup();
        var member = Store.Rows.OfType<SchoolMembership>().First();
        var branch = new Branch { SchoolId = School, BranchId = Guid.NewGuid() };
        Store.Rows.Add(branch);
        var id = kind switch
        {
            "Group" => Group,
            "Enrollment" => Enrollment,
            "Membership" => member.MembershipId,
            "Branch" => branch.BranchId,
            _ => School
        };
        var count = Store.Rows.Count;
        Error(async () => await Service.RejectActivationAsync(School, kind, id, Guid.NewGuid(), 1, Admin, default), "ACTIVATION_INTEGRATION_NOT_READY");
        Assert.That(Store.Rows.Count, Is.EqualTo(count));
        Assert.That(Store.Commits, Is.Zero);
    }
    [Test]
    public void Gate_rejection_still_hides_missing_targets()
    {
        Error(async () => await Service.RejectActivationAsync(School, "Enrollment", Guid.NewGuid(), Guid.NewGuid(), 1, Admin, default), "NOT_FOUND");
    }
    [Test]
    public async Task Same_operation_UUID_is_independent_between_schools()
    {
        var first = School;
        var second = Guid.NewGuid();
        Store.Rows.Add(new School { SchoolId = second, Name = "Other", Status = SchoolStatus.Active });
        Access.PlatformOperatorUserIds = [AdminUser];
        var op = Guid.NewGuid();
        await Service.SaveBranchAsync(first, null, op, new("First"), null, Admin, default);
        await Service.SaveBranchAsync(second, null, op, new("Second"), null, Admin, default);
        Assert.That(Store.Rows.OfType<SchoolOperation>().Count(), Is.EqualTo(2));
    }
}
