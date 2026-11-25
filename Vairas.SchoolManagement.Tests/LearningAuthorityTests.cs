using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

/// <summary>US-023 / AC-059-061: an instructor reads Learning data of an enrollment only through a current assignment.</summary>
[TestFixture,Category("Unit")]
public sealed class LearningAuthorityTests : BusinessFixture
{
    private SchoolLearningService Learning => new(Store,Activity,Clock);
    private TeacherGroupAssignment Assigned(Guid group)
    {
        var assignment=new TeacherGroupAssignment{AssignmentId=Guid.NewGuid(),SchoolId=School,GroupId=group,TeacherId=Teacher,AssignedAt=Clock.Now.AddDays(-1)};
        Store.Rows.Add(assignment);return assignment;
    }
    private new SchoolMembership Member(Guid user, SchoolRole role)
    { if (role == SchoolRole.Teacher) Activity.Bindings[Teacher]=new("Teacher",Teacher,user,true); return base.Member(user,role); }
    private void InGroup() =>Store.Rows.Add(new StudentGroupMembership{MembershipId=Guid.NewGuid(),SchoolId=School,EnrollmentId=Enrollment,GroupId=Group,JoinedAt=Clock.Now.AddDays(-1)});
    private Task<LearningAuthorityResponse> Read() => Learning.AuthorityAsync(new(School,Target,Enrollment,Teacher),default);

    [Test] public async Task Currently_assigned_teacher_is_admitted_without_administrator_or_assessor_rights()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();Member(Target,SchoolRole.Teacher);Assigned(Group);
        var a=await Read();
        Assert.That(a.Allowed,Is.True);Assert.That(a.AssignedTeacher,Is.True);
        Assert.That(a.Administrator,Is.False);Assert.That(a.Assessor,Is.False);Assert.That(a.Self,Is.False);
    }
    [Test] public void Unassigned_teacher_is_denied_without_confirming_the_enrollment()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();Member(Target,SchoolRole.Teacher);Assigned(Guid.NewGuid());
        Error(()=>Read(),"NOT_FOUND");
    }
    [Test] public void Ended_and_revoked_assignments_no_longer_admit()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();var membership=Member(Target,SchoolRole.Teacher);var assignment=Assigned(Group);
        assignment.EndedAt=Clock.Now.AddMinutes(-1);
        Error(()=>Read(),"NOT_FOUND");
        assignment.EndedAt=null;membership.EndedAt=Clock.Now.AddMinutes(-1);
        Error(()=>Read(),"NOT_FOUND");
    }
    [Test] public void Previous_group_of_the_enrollment_does_not_admit_its_former_teacher()
    {
        Enroll();TrainingGroup().CategoryCode="B";Member(Target,SchoolRole.Teacher);Assigned(Group);
        Store.Rows.Add(new StudentGroupMembership{MembershipId=Guid.NewGuid(),SchoolId=School,EnrollmentId=Enrollment,GroupId=Group,JoinedAt=Clock.Now.AddDays(-3),LeftAt=Clock.Now.AddDays(-2)});
        Error(()=>Read(),"NOT_FOUND");
    }
    [Test] public void Assignment_in_another_school_does_not_admit()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();Member(Target,SchoolRole.Teacher);Assigned(Group);
        Error(()=>Learning.AuthorityAsync(new(Guid.NewGuid(),Target,Enrollment,Teacher),default),"NOT_FOUND");
    }
    [Test] public void Teacher_cannot_borrow_another_teachers_assignment()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();Member(Target,SchoolRole.Teacher);Assigned(Group);
        var foreign=Guid.NewGuid();Activity.Bindings[foreign]=new("Teacher",foreign,Other,true);
        Error(()=>Learning.AuthorityAsync(new(School,Target,Enrollment,foreign),default),"NOT_FOUND");
        // A different teacher member presenting the assigned teacher's profile id is not admitted either.
        base.Member(Other,SchoolRole.Teacher);
        Error(()=>Learning.AuthorityAsync(new(School,Other,Enrollment,Teacher),default),"NOT_FOUND");
    }
    [Test] public async Task Student_reads_and_confirms_only_through_the_own_enrollment_binding()
    {
        // US-013 / AC-033, US-014 / AC-036: Learning admits a student only for the enrollment bound to the caller's own profile.
        Enroll();base.Member(Target,SchoolRole.Student);base.Member(Other,SchoolRole.Student);
        Activity.Bindings[Student]=new("Student",Student,Target,true);
        var own=await Learning.AuthorityAsync(new(School,Target,Enrollment),default);
        Assert.That((own.Allowed,own.Self,own.Administrator,own.Teacher),Is.EqualTo((true,true,false,false)));
        Error(()=>Learning.AuthorityAsync(new(School,Other,Enrollment),default),"NOT_FOUND");
        Error(()=>Learning.AuthorityAsync(new(Guid.NewGuid(),Target,Enrollment),default),"NOT_FOUND");
        Activity.Bindings[Student]=new("Student",Student,Target,false);
        Error(()=>Learning.AuthorityAsync(new(School,Target,Enrollment),default),"NOT_FOUND");
    }
    [Test] public void Inactive_teacher_profile_binding_does_not_admit()
    {
        Enroll();TrainingGroup().CategoryCode="B";InGroup();Member(Target,SchoolRole.Teacher);Assigned(Group);
        Activity.Bindings[Teacher]=new("Teacher",Teacher,Target,false);
        Error(()=>Read(),"NOT_FOUND");
    }
}
