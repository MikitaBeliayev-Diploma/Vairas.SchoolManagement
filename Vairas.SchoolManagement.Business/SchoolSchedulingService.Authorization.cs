using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;
public sealed partial class SchoolSchedulingService(ISchoolStore store,IUserActivity activity,SchoolAccessOptions access,ISchoolSchedulingClient scheduling,TimeProvider clock,ILearningCompletionClient? learning=null):ISchoolSchedulingService
{
    private bool Operator(Guid user)=>access.PlatformOperatorUserIds.Contains(user);
    private static bool Current(SchoolMembership x,DateTimeOffset now)=>SchoolRules.Effective(x,now);
    private static bool Grant(MembershipPermissionGrant x,DateTimeOffset now)=>x.StartedAt<=now && (x.EndedAt is null || x.EndedAt>now);
    private async Task<(School Row,SchoolMembership[] Roles,bool Admin)> Scope(ISchoolSession s,Guid school,Guid actor,CancellationToken ct)
    {
        var now=await s.NowAsync(ct);var row=await s.FindAsync<School>(x=>x.SchoolId==school && x.Status==SchoolStatus.Active,ct)??throw SchoolErrors.Missing();
        if((await s.ListAsync<SchoolOperation>(x=>x.SchoolId==school && x.CoordinationRequired && (x.Status==OperationStatus.Pending || x.Status==OperationStatus.ReconciliationPending),ct)).Count>0)throw SchoolErrors.Dependency();
        var roles=(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==actor,ct)).Where(x=>Current(x,now)).ToArray();
        if(roles.Length==0 && !Operator(actor))throw SchoolErrors.Missing();
        return (row,roles,roles.Any(x=>x.Role==SchoolRole.Administrator));
    }
    public async Task<SchedulingSnapshot> AccessAsync(Guid school,Guid actor,CancellationToken ct)
    {
        SchoolRules.Id(school);SchoolRules.Id(actor);var started=clock.GetUtcNow();await activity.RequireActiveAsync(actor,ct);
        await using var s=await store.OpenAsync(school,false,ct);var (row,roles,admin)=await Scope(s,school,actor,ct);var now=await s.NowAsync(ct);
        var ids=roles.Select(x=>x.MembershipId).ToArray();var grants=await s.ListAsync<MembershipPermissionGrant>(x=>x.SchoolId==school && ids.Contains(x.MembershipId),ct);
        return new(school,row.Revision,actor,Guid.Empty,Guid.Empty,true,admin,admin && grants.Any(x=>x.Permission==Permission.SchoolRuleOwner && Grant(x,now)),
            admin && access.LessonCorrectionUserIds.Contains(actor),false,roles.Any(x=>x.Role==SchoolRole.Teacher),null,null,null,null,"",[],default,default,started.AddSeconds(30));
    }
    public async Task<SchedulingSnapshot> SnapshotAsync(SchedulingSnapshotRequest value,CancellationToken ct)
    {
        var r=value.Request;SchoolRules.Id(r.SchoolId);SchoolRules.Id(value.ActorUserId);SchoolRules.Id(r.TeacherId);SchoolRules.Id(r.BranchId);SchoolRules.Interval(r.StartsAt,r.EndsAt);
        var started=clock.GetUtcNow();await activity.RequireActiveAsync(value.ActorUserId,ct);
        var teacher=await activity.BindingAsync("Teacher",r.TeacherId,ct);if(!teacher.Active)throw SchoolErrors.Denied();
        ProfileBinding? student=r.StudentId is Guid studentId?await activity.BindingAsync("Student",studentId,ct):null;
        if(student is not null && !student.Active)throw SchoolErrors.Denied();
        await using var s=await store.OpenAsync(r.SchoolId,false,ct);var (school,roles,admin)=await Scope(s,r.SchoolId,value.ActorUserId,ct);var now=await s.NowAsync(ct);
        var branch=await s.FindAsync<Branch>(x=>x.SchoolId==r.SchoolId && x.BranchId==r.BranchId && x.Status==SchoolStatus.Active,ct)??throw SchoolErrors.Missing();
        var teacherRoles=await s.ListAsync<SchoolMembership>(x=>x.SchoolId==r.SchoolId && x.UserId==teacher.UserId && x.Role==SchoolRole.Teacher,ct);
        if(!teacherRoles.Any(x=>Current(x,now) && SchoolRules.Covers(x.StartedAt,x.EndedAt,r.StartsAt,r.EndsAt)))throw SchoolErrors.Denied();
        var participants=Array.Empty<SchedulingParticipant>();long? membership=null;
        if(r.EnrollmentId is Guid enrollment)
        {
            var e=await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==r.SchoolId && x.EnrollmentId==enrollment,ct)??throw SchoolErrors.Missing();
            if(student is null || e.StudentId!=r.StudentId || e.CategoryCode!=r.Category || e.Status!=EnrollmentStatus.Active || !SchoolRules.Covers(e.StartedAt,e.EndedAt,r.StartsAt,r.EndsAt))throw SchoolErrors.Denied();
            if(!(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==r.SchoolId && x.UserId==student.UserId && x.Role==SchoolRole.Student,ct)).Any(x=>SchoolRules.Covers(x.StartedAt,x.EndedAt,r.StartsAt,r.EndsAt)))throw SchoolErrors.Denied();
            if(!admin && !(value.ActorUserId==student.UserId && roles.Any(x=>x.Role==SchoolRole.Student)) && !(value.ActorUserId==teacher.UserId && roles.Any(x=>x.Role==SchoolRole.Teacher)))throw SchoolErrors.Missing();
        }
        else if(r.GroupId is Guid group)
        {
            var g=await s.FindAsync<TrainingGroup>(x=>x.SchoolId==r.SchoolId && x.GroupId==group,ct)??throw SchoolErrors.Missing();
            if(g.BranchId!=branch.BranchId || g.CategoryCode!=r.Category || !SchoolRules.Covers(g.StartedAt,g.EndedAt,r.StartsAt,r.EndsAt))throw SchoolErrors.Denied();
            var assignments=await s.ListAsync<TeacherGroupAssignment>(x=>x.SchoolId==r.SchoolId && x.GroupId==group && x.TeacherId==r.TeacherId,ct);
            if(!assignments.Any(x=>SchoolRules.Covers(x.AssignedAt,x.EndedAt,r.StartsAt,r.EndsAt)))throw SchoolErrors.Denied();
            var roster=await s.ListAsync<GroupRosterMember>(x=>x.SchoolId==r.SchoolId && x.GroupId==group && x.MembershipRevision==g.MembershipRevision,ct);
            participants=roster.Where(x=>SchoolRules.Covers(x.JoinedAt,x.LeftAt,r.StartsAt,r.EndsAt) && SchoolRules.Covers(x.EnrollmentStartedAt,x.EnrollmentEndedAt,r.StartsAt,r.EndsAt)).Select(x=>new SchedulingParticipant(x.EnrollmentId,x.StudentId)).ToArray();
            if(participants.Length==0 || participants.Select(x=>x.StudentId).Distinct().Count()!=participants.Length)throw SchoolErrors.Conflict("AMBIGUOUS_PARTICIPANT");
            var isParticipant=false;
            foreach(var participant in participants)
            {
                var binding=await activity.BindingAsync("Student",participant.StudentId,ct);
                if(!binding.Active)throw SchoolErrors.Denied();
                await activity.RequireActiveAsync(binding.UserId,ct);
                if(!(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==r.SchoolId && x.UserId==binding.UserId && x.Role==SchoolRole.Student,ct)).Any(x=>SchoolRules.Covers(x.StartedAt,x.EndedAt,r.StartsAt,r.EndsAt)))throw SchoolErrors.Denied();
                if(binding.UserId==value.ActorUserId && roles.Any(x=>x.Role==SchoolRole.Student))isParticipant=true;
            }
            if(!admin && value.ActorUserId!=teacher.UserId && !isParticipant)throw SchoolErrors.Missing();
            membership=g.MembershipRevision;
        }
        else throw SchoolErrors.Invalid();
        var teacherMembershipIds=teacherRoles.Where(x=>Current(x,now)).Select(x=>x.MembershipId).ToArray();
        var assessorGrants=await s.ListAsync<MembershipPermissionGrant>(x=>x.SchoolId==r.SchoolId && teacherMembershipIds.Contains(x.MembershipId) && x.Permission==Permission.Assessor,ct);
        var teacherAssessor=assessorGrants.Any(x=>Grant(x,now) && SchoolRules.Covers(x.StartedAt,x.EndedAt,r.StartsAt,r.EndsAt));
        var own=roles.Select(x=>x.MembershipId).ToArray();var grants=await s.ListAsync<MembershipPermissionGrant>(x=>x.SchoolId==r.SchoolId && own.Contains(x.MembershipId),ct);
        return new(r.SchoolId,school.Revision,value.ActorUserId,r.TeacherId,r.BranchId,true,admin,admin && grants.Any(x=>x.Permission==Permission.SchoolRuleOwner && Grant(x,now)),admin && access.LessonCorrectionUserIds.Contains(value.ActorUserId),teacherAssessor,roles.Any(x=>x.Role==SchoolRole.Teacher),
            r.EnrollmentId,r.StudentId,r.GroupId,membership,r.Category,participants,r.StartsAt,r.EndsAt,started.AddSeconds(30));
    }
    public async Task<RuleScopeResponse> RuleScopeAsync(RuleScopeRequest r,CancellationToken ct)
    {
        var authority=await AccessAsync(r.SchoolId,r.ActorUserId,ct);if(!authority.RuleOwner)throw SchoolErrors.Denied();
        await using var s=await store.OpenAsync(r.SchoolId,false,ct);
        var known=r.ScopeKind switch
        {
            "School"=>r.ScopeResourceId is null,
            "Branch"=>await s.FindAsync<Branch>(x=>x.SchoolId==r.SchoolId && x.BranchId==r.ScopeResourceId,ct) is not null,
            "Group"=>await s.FindAsync<TrainingGroup>(x=>x.SchoolId==r.SchoolId && x.GroupId==r.ScopeResourceId,ct) is not null,
            "Student"=> (await s.ListAsync<StudentEnrollment>(x=>x.SchoolId==r.SchoolId && x.StudentId==r.ScopeResourceId,ct)).Count>0,
            "Instructor"=>false, // Resolved below through authoritative global binding and School membership.
            _=>false
        };
        if(r.ScopeKind=="Instructor" && r.ScopeResourceId is Guid teacher)
        {var binding=await activity.BindingAsync("Teacher",teacher,ct);known=binding.Active && (await s.ListAsync<SchoolMembership>(x=>x.SchoolId==r.SchoolId && x.UserId==binding.UserId && x.Role==SchoolRole.Teacher,ct)).Any(x=>Current(x,clock.GetUtcNow()));}
        if(!known)throw SchoolErrors.Denied();
        return new(true,r.SchoolId,r.ActorUserId,r.ScopeKind,r.ScopeResourceId);
    }
}
