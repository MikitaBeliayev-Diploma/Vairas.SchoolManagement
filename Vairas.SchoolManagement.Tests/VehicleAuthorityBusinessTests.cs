using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class VehicleAuthorityBusinessTests : BusinessFixture
{
    private Guid Branch;
    [SetUp] public void VehicleSetup()
    {
        Branch = Guid.NewGuid();
        Store.Rows.Add(new Branch { BranchId = Branch, SchoolId = School, Name = "Synthetic", Status = SchoolStatus.Active, Revision = 1 });
        Activity.Bindings[Teacher] = new("Teacher", Teacher, Target, true);
    }
    [Test] public async Task Current_administrator_and_exact_teacher_membership_are_echoed_without_writes()
    {
        var membership = Member(Target, SchoolRole.Teacher);
        membership.EndedAt = Clock.Now.AddSeconds(10);
        var result = await Service.VehicleAuthorityAsync(new(School, Branch, AdminUser, Teacher, membership.MembershipId), default);
        Assert.That(result.Allowed, Is.True); Assert.That(result.SchoolMembershipId, Is.EqualTo(membership.MembershipId));
        Assert.That(result.TeacherUserId, Is.EqualTo(Target)); Assert.That(result.ValidUntil, Is.EqualTo(membership.EndedAt));
        Assert.That(Store.Commits, Is.Zero);
    }
    [Test] public async Task Rejoined_membership_does_not_restore_old_assignment()
    {
        Member(Target, SchoolRole.Teacher);
        var result = await Service.VehicleAuthorityAsync(new(School, Branch, null, Teacher, Guid.NewGuid()), default);
        Assert.That(result.Allowed, Is.False); Assert.That(result.TeacherUserId, Is.Null); Assert.That(result.SchoolMembershipId, Is.Null);
    }
    [Test] public async Task Profile_without_school_role_and_inactive_source_are_denied()
    {
        Assert.That((await Service.VehicleAuthorityAsync(new(School, Branch, Other), default)).Allowed, Is.False);
        Member(Target, SchoolRole.Teacher); Activity.Inactive.Add(Target);
        Assert.That((await Service.VehicleAuthorityAsync(new(School, Branch, null, Teacher), default)).Allowed, Is.False);
    }
    [Test] public async Task Wrong_branch_inactive_school_and_pending_coordination_are_not_authority()
    {
        Assert.That((await Service.VehicleAuthorityAsync(new(School, Guid.NewGuid(), AdminUser), default)).Allowed, Is.False);
        Store.Rows.OfType<School>().Single().Status = SchoolStatus.Inactive;
        Assert.That((await Service.VehicleAuthorityAsync(new(School, Branch, AdminUser), default)).Allowed, Is.False);
        Store.Rows.Add(new SchoolOperation { SchoolId = School, CoordinationRequired = true, Status = OperationStatus.Pending });
        Assert.That(Business.ThrowsBusinessAsync(async () => await Service.VehicleAuthorityAsync(new(School, Branch, AdminUser), default)).HttpStatus(), Is.EqualTo(503));
    }
    [Test] public void Invalid_membership_shape_is_rejected() =>
        Business.ThrowsBusinessAsync(async () => await Service.VehicleAuthorityAsync(new(School, Branch, null, null, Guid.NewGuid()), default));
}
