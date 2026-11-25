using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;
[TestFixture,Category("Unit")]
public sealed class SchedulingCoordinationTests:BusinessFixture
{
    private MemoryScheduling Client=null!;private SchoolSchedulingService Coordinator=null!;
    [SetUp]public void Configure(){Client=new();Access.PlatformOperatorUserIds=[AdminUser];Coordinator=new(Store,Activity,Access,Client,Clock);}
    [Test]
    public async Task Enrollment_activation_is_recoverable_and_not_effective_before_Scheduling_finalization()
    {
        Activity.Bindings[Student]=new("Student",Student,Target,true);var operation=Guid.NewGuid();var command=new SchoolChangeRequest("CreateEnrollment",School,StudentId:Student,Category:"B");Client.FailFinish=true;
        Business.ThrowsBusinessAsync(async()=>await Coordinator.ChangeAsync(School,operation,command,Admin,default));
        var pending=await Coordinator.OutcomeAsync(School,operation,Admin,default);Assert.That(pending.State,Is.EqualTo("LocalApplied"));
        Business.ThrowsBusinessAsync(async()=>await Coordinator.AccessAsync(School,AdminUser,default));
        Client.FailFinish=false;var result=await Coordinator.ResumeAsync(School,operation,default);Assert.That(result.State,Is.EqualTo("Committed"));
        Assert.That((await Coordinator.ChangeAsync(School,operation,command,Admin,default)).ResultId,Is.EqualTo(result.ResultId));
        Assert.That(Store.Rows.OfType<StudentEnrollment>().Count(),Is.EqualTo(1));Assert.That((await Coordinator.DecisionAsync(School,operation,default)).LocalApplied,Is.True);
    }
    [Test]
    public async Task First_administrator_invitation_uses_durable_activation()
    {
        var issued=await Service.IssueAsync(School,Guid.NewGuid(),new(Target,SchoolRole.Administrator),Admin,default);
        var accepted=await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("AcceptInvitation",issued.Resource.InvitationId,1),Actor(Target),default);
        Assert.That(accepted.State,Is.EqualTo("Committed"));Assert.That(Store.Rows.OfType<SchoolMembership>().Any(x=>x.UserId==Target && x.Role==SchoolRole.Administrator),Is.True);
    }
    [Test]
    public async Task Reusing_another_commands_idempotency_key_is_a_conflict_not_a_server_failure()
    {
        // Found by the live acceptance run: accepting with the key that issued the invitation produced HTTP 500.
        var key=Guid.NewGuid();
        var issued=await Service.IssueAsync(School,key,new(Target,SchoolRole.Administrator),Admin,default);
        var error=Business.ThrowsBusinessAsync(async()=>await Coordinator.ChangeAsync(School,key,new("AcceptInvitation",issued.Resource.InvitationId,1),Actor(Target),default));
        Assert.That((error.HttpStatus(),error.Code),Is.EqualTo((409,"IDEMPOTENCY_KEY_REUSED")));
        Assert.That(Store.Rows.OfType<SchoolCoordination>(),Is.Empty);
        Assert.That(Store.Rows.OfType<SchoolMembership>().Any(x=>x.UserId==Target),Is.False);
        Assert.That((await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("AcceptInvitation",issued.Resource.InvitationId,1),Actor(Target),default)).State,Is.EqualTo("Committed"));
    }
    [Test]
    public async Task Permission_grants_are_platform_only_role_bound_and_revocable()
    {
        var membership=Store.Rows.OfType<SchoolMembership>().Single(x=>x.UserId==AdminUser);var op=Guid.NewGuid();
        var grant=await Coordinator.ChangeAsync(School,op,new("GrantPermission",membership.MembershipId,Permission:"SchoolRuleOwner"),Admin,default);
        Assert.That((await Coordinator.AccessAsync(School,AdminUser,default)).RuleOwner,Is.True);
        await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("RevokePermission",grant.ResultId,Reason:"Revoked"),Admin,default);
        Assert.That((await Coordinator.AccessAsync(School,AdminUser,default)).RuleOwner,Is.False);
        var teacher=Member(Target,SchoolRole.Teacher);Access.PlatformOperatorUserIds=[];
        Business.ThrowsBusinessAsync(async()=>await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("GrantPermission",teacher.MembershipId,Permission:"Assessor"),Admin,default));
    }
    [Test]
    public async Task Group_changes_prepare_complete_rosters_and_preserve_historical_revisions()
    {
        Activity.Bindings[Student]=new("Student",Student,Target,true);
        var enrollment=await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("CreateEnrollment",School,StudentId:Student,Category:"B"),Admin,default);
        Store.Rows.Add(new TrainingGroup {GroupId=Group,SchoolId=School,CategoryCode="B",Name="Group",Revision=1,MembershipRevision=1,StartedAt=Clock.Now.AddDays(-1)});
        Store.Rows.Add(new GroupRosterRevision {SchoolId=School,GroupId=Group,MembershipRevision=1,RecordedAt=Clock.Now});
        Client.Impacts=[new(Guid.NewGuid(),School,"Theory","Scheduled",1,Clock.Now.AddDays(1),Clock.Now.AddDays(1).AddMinutes(45),null,null,Teacher,null,null,null,Group,1)];
        await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("AssignGroup",enrollment.ResultId,1,GroupId:Group,ProgramVersionId:Guid.NewGuid()),Admin,default);
        Assert.That(Client.Transitions.Values.Last().Rosters.Single().Participants.Single().StudentId,Is.EqualTo(Student));
        Assert.That(Store.Rows.OfType<GroupRosterRevision>().Count(),Is.EqualTo(2));
        // Removal when no future lessons remain still writes a new retained empty basis.
        Client.Impacts=[];
        await Coordinator.ChangeAsync(School,Guid.NewGuid(),new("RemoveGroup",enrollment.ResultId,2,GroupId:Group),Admin,default);
        Assert.That(Store.Rows.OfType<StudentGroupMembership>().Single().LeftAt,Is.Not.Null);
        Assert.That(Store.Rows.OfType<GroupRosterRevision>().Count(),Is.EqualTo(3));
    }
    [Test]
    public async Task Student_and_teacher_snapshots_require_exact_scope_interval_and_bindings()
    {
        var branch=Guid.NewGuid();Member(Target,SchoolRole.Teacher);Member(Other,SchoolRole.Student);
        Activity.Bindings[Teacher]=new("Teacher",Teacher,Target,true);Activity.Bindings[Student]=new("Student",Student,Other,true);
        Store.Rows.Add(new Branch {SchoolId=School,BranchId=branch,Name="Branch",Status=SchoolStatus.Active,Revision=1});
        Store.Rows.Add(new StudentEnrollment {SchoolId=School,EnrollmentId=Enrollment,StudentId=Student,CategoryCode="B",Status=EnrollmentStatus.Active,StartedAt=Clock.Now.AddDays(-1),Revision=1});
        var input=new SchedulingInput(School,branch,Teacher,"B",Clock.Now.AddDays(1),Clock.Now.AddDays(1).AddMinutes(45),Enrollment,Student,Guid.NewGuid());
        var result=await Coordinator.SnapshotAsync(new(input,AdminUser),default);Assert.That(result.StudentId,Is.EqualTo(Student));
        Business.ThrowsBusinessAsync(async()=>await Coordinator.SnapshotAsync(new(input with {StudentId=Guid.NewGuid()},AdminUser),default));
        Business.ThrowsBusinessAsync(async()=>await Coordinator.SnapshotAsync(new(input with {Category="C"},AdminUser),default));
    }
    [Test]
    public async Task Deterministic_Scheduling_rejection_does_not_apply_School_changes()
    {
        Client.RejectPrepare=true;Activity.Bindings[Student]=new("Student",Student,Target,true);
        var operation=Guid.NewGuid();Business.ThrowsBusinessAsync(async()=>await Coordinator.ChangeAsync(School,operation,new("CreateEnrollment",School,StudentId:Student,Category:"B"),Admin,default));
        Assert.That(Store.Rows.OfType<StudentEnrollment>(),Is.Empty);Assert.That((await Coordinator.OutcomeAsync(School,operation,Admin,default)).State,Is.EqualTo("Aborted"));
    }
    [Test]
    public async Task Operator_abort_of_unknown_prepare_requires_ack_and_keeps_its_audited_reason()
    {
        Activity.Bindings[Student]=new("Student",Student,Target,true);Client.FailPrepare=true;var operation=Guid.NewGuid();
        Business.ThrowsBusinessAsync(async()=>await Coordinator.ChangeAsync(School,operation,new("CreateEnrollment",School,StudentId:Student,Category:"B"),Admin,default));
        Client.FailFinish=true;
        Business.ThrowsBusinessAsync(async()=>await Coordinator.AbortAsync(School,operation,"Operator verified no local commit",Admin,default));
        Assert.That((await Coordinator.OutcomeAsync(School,operation,Admin,default)).State,Is.EqualTo("AbortPending"));
        Assert.That((await Coordinator.DecisionAsync(School,operation,default)).State,Is.EqualTo("Aborted"));
        Client.FailFinish=false;var result=await Coordinator.ResumeAsync(School,operation,default);Assert.That(result.State,Is.EqualTo("Aborted"));Assert.That(result.SchoolRevision,Is.EqualTo(1));
        Assert.That(Store.Rows.OfType<StudentEnrollment>(),Is.Empty);Assert.That(Store.Rows.OfType<SchoolOperation>().Single(x=>x.CommandName=="AbortCoordination").Reason,Is.EqualTo("Operator verified no local commit"));
    }
    [TestCase("SchoolStatus"),TestCase("BranchStatus"),TestCase("EndMembership"),TestCase("AssignTeacher"),TestCase("EndEnrollment")]
    public async Task Eligibility_changes_advance_the_School_revision_under_one_operation(string kind)
    {
        var target=School;SchoolChangeRequest command;
        switch(kind)
        {
            case "BranchStatus":target=Guid.NewGuid();Store.Rows.Add(new Branch{SchoolId=School,BranchId=target,Status=SchoolStatus.Active,Revision=1,Name="Branch"});command=new(kind,target,1,Status:"Inactive");break;
            case "EndMembership":target=Member(Target,SchoolRole.Teacher).MembershipId;command=new(kind,target,1);break;
            case "AssignTeacher":Member(Target,SchoolRole.Teacher);Activity.Bindings[Teacher]=new("Teacher",Teacher,Target,true);target=Group;Store.Rows.Add(new TrainingGroup{SchoolId=School,GroupId=Group,Name="G",Revision=1,MembershipRevision=1,StartedAt=Clock.Now});command=new(kind,target,1,TeacherId:Teacher);break;
            case "EndEnrollment":target=Enrollment;Store.Rows.Add(new StudentEnrollment{SchoolId=School,EnrollmentId=Enrollment,StudentId=Student,CategoryCode="B",Status=EnrollmentStatus.Active,Revision=1,StartedAt=Clock.Now.AddDays(-1)});command=new(kind,target,1,Status:"Withdrawn");break;
            default:command=new(kind,target,1,Status:"Inactive");break;
        }
        var result=await Coordinator.ChangeAsync(School,Guid.NewGuid(),command,Admin,default);Assert.That(result.State,Is.EqualTo("Committed"));Assert.That(Store.Rows.OfType<School>().Single().Revision,Is.EqualTo(2));
    }
}
