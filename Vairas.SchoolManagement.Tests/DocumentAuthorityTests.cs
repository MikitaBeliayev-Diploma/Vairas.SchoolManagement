using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture,Category("Unit")]
public sealed class DocumentAuthorityTests : BusinessFixture
{
    private SchoolDocumentService Documents => new(Store,Activity,Clock);
    [Test] public async Task Administrator_authority_requires_fresh_activity_and_exact_school()
    {
        Enroll();var a=await Documents.AuthorityAsync(new(School,AdminUser,Enrollment,null),default);Assert.That(a.Administrator,Is.True);Assert.That(a.StudentId,Is.EqualTo(Student));
        Activity.Inactive.Add(AdminUser);Error(()=>Documents.AuthorityAsync(new(School,AdminUser,Enrollment,null),default),"ACCESS_DENIED");
        Activity.Inactive.Clear();Error(()=>Documents.AuthorityAsync(new(Guid.NewGuid(),AdminUser,Enrollment,null),default),"NOT_FOUND");
    }
    [Test] public async Task Student_access_is_bound_to_current_global_profile_and_membership()
    {
        Enroll();Member(Target,SchoolRole.Student);Activity.Bindings[Student]=new("Student",Student,Target,true);
        Assert.That((await Documents.AuthorityAsync(new(School,Target,Enrollment,null),default)).Self,Is.True);
        var scopeOnly=await Documents.AuthorityAsync(new(School,Target,null,null),default);Assert.That(scopeOnly.EnrollmentId,Is.Null);Assert.That(scopeOnly.Self,Is.False);Assert.That(scopeOnly.AssignedTeacher,Is.False);
        Activity.Bindings[Student]=new("Student",Student,Other,true);Error(()=>Documents.AuthorityAsync(new(School,Target,Enrollment,null),default),"NOT_FOUND");
    }
    [Test] public async Task Teacher_requires_own_active_binding_and_current_assignment_to_the_enrollment_group()
    {
        Enroll();Member(Target,SchoolRole.Teacher);Activity.Bindings[Teacher]=new("Teacher",Teacher,Target,true);
        var association=new StudentGroupMembership{MembershipId=Guid.NewGuid(),SchoolId=School,EnrollmentId=Enrollment,GroupId=Group,JoinedAt=Clock.Now.AddDays(-1)};
        var assignment=new TeacherGroupAssignment{AssignmentId=Guid.NewGuid(),SchoolId=School,GroupId=Group,TeacherId=Teacher,AssignedAt=Clock.Now.AddDays(-1)};Store.Rows.Add(association);Store.Rows.Add(assignment);
        Assert.That((await Documents.AuthorityAsync(new(School,Target,Enrollment,Teacher),default)).AssignedTeacher,Is.True);
        assignment.EndedAt=Clock.Now;Error(()=>Documents.AuthorityAsync(new(School,Target,Enrollment,Teacher),default),"NOT_FOUND");
        assignment.EndedAt=null;Activity.Bindings[Teacher]=new("Teacher",Teacher,Other,true);Error(()=>Documents.AuthorityAsync(new(School,Target,Enrollment,Teacher),default),"NOT_FOUND");
    }
    [Test] public void Waiting_for_store_does_not_extend_document_admission()
    { Store.OnOpen=()=>Clock.Now=Clock.Now.AddSeconds(31);Error(()=>Documents.AuthorityAsync(new(School,AdminUser,null,null),default),"DEPENDENCY_UNAVAILABLE"); }
}
