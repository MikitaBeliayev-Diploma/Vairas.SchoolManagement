using System.Security.Cryptography;
using System.Text;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;
public sealed partial class SchoolSchedulingService
{
    private static SchoolChangeResponse Response(SchoolCoordination x)=>new(x.OperationId,x.SchoolId,x.State,x.ResultId,x.State is "LocalApplied" or "Committed"?x.AfterRevision:x.BeforeRevision);
    public async Task<SchoolChangeResponse> ChangeAsync(Guid school,Guid operation,SchoolChangeRequest request,SchoolActor actor,CancellationToken ct)
    {
        SchoolRules.Id(school);SchoolRules.Id(operation);SchoolRules.Fresh(actor,clock.GetUtcNow());
        if(!scheduling.Enabled)throw SchoolErrors.Gate();
        var fingerprint=SchoolRules.Fingerprint("Coordinate:"+request.Kind,request.TargetId,request,actor);
        await activity.RequireActiveAsync(actor.UserId,ct);
        await using(var replay=await store.OpenAsync(school,false,ct))
        {
            var saved=await replay.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct);
            if(saved is not null)
            {
                if(saved.Fingerprint!=fingerprint || saved.ActorUserId!=actor.UserId)throw SchoolErrors.Conflict("IDEMPOTENCY_KEY_REUSED");
                return await ResumeAsync(school,operation,ct);
            }
        }
        Guid? targetUser=null;
        if(request.StudentId is Guid student){var binding=await activity.BindingAsync("Student",student,ct);if(!binding.Active)throw SchoolErrors.Denied();targetUser=binding.UserId;}
        if(request.TeacherId is Guid teacher){var binding=await activity.BindingAsync("Teacher",teacher,ct);if(!binding.Active)throw SchoolErrors.Denied();targetUser=binding.UserId;}
        if(request.Kind=="AcceptInvitation")
        {
            await using var read=await store.OpenAsync(school,false,ct);
            var invitation=await read.FindAsync<SchoolInvitation>(x=>x.SchoolId==school && x.InvitationId==request.TargetId && x.TargetUserId==actor.UserId,ct)??throw SchoolErrors.Missing();
            var issuer=await read.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==invitation.IssuedOperationId,ct)??throw SchoolErrors.Missing();
            await activity.RequireActiveAsync(issuer.ActorUserId,ct);
            if(invitation.Role==SchoolRole.Student && actor.StudentId is null || invitation.Role==SchoolRole.Teacher && actor.TeacherId is null || invitation.Role==SchoolRole.Administrator && actor.AdministratorId is null)throw new UnprocessableEntityException("PROFILE_REQUIRED","Ensure the required role profile first.");
            request=request with {StudentId=invitation.Role==SchoolRole.Student?actor.StudentId:null,UserId=actor.UserId,Status=invitation.Role.ToString(),Category=invitation.CategoryCode};
        }
        else request=request with {UserId=targetUser};
        if(request.Kind=="EndMembership")request=await MembershipResource(school,request,actor,ct);
        if(request.Kind=="AssignGroup")
        {
            if(request.GroupId is not Guid group || request.ProgramVersionId is not Guid programme)throw SchoolErrors.Invalid();
            await scheduling.ValidateProgrammeAsync(school,request.TargetId,group,programme,ct);
        }
        if(request.Kind=="EndEnrollment" && request.Status=="Completed")
        {
            if(learning is null)throw SchoolErrors.Dependency();
            await learning.RequireCurrentCompletionAsync(school,request.TargetId,ct);
        }
        if(request.Kind is "GrantPermission" or "RevokePermission")
        {
            if(!Operator(actor.UserId))throw SchoolErrors.Denied();
            await using var read=await store.OpenAsync(school,false,ct);
            var membership=request.Kind=="GrantPermission"?await read.FindAsync<SchoolMembership>(x=>x.SchoolId==school && x.MembershipId==request.TargetId,ct):null;
            if(membership is not null)await activity.RequireActiveAsync(membership.UserId,ct);
        }
        await using(var s=await store.OpenAsync(school,true,ct))
        {
            var old=await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct);
            if(old is not null)
            {if(old.Fingerprint!=fingerprint || old.ActorUserId!=actor.UserId)throw SchoolErrors.Conflict("IDEMPOTENCY_KEY_REUSED");}
            else
            {
                // An Idempotency-Key already used by a different School command is a client error (409), never a server failure.
                if(await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct) is not null)throw SchoolErrors.Conflict("IDEMPOTENCY_KEY_REUSED");
                var row=await s.FindAsync<School>(x=>x.SchoolId==school,ct)??throw SchoolErrors.Missing();var now=await s.NowAsync(ct);
                if((await s.ListAsync<SchoolOperation>(x=>x.SchoolId==school && x.CoordinationRequired && (x.Status==OperationStatus.Pending || x.Status==OperationStatus.ReconciliationPending),ct)).Count>0)throw SchoolErrors.Dependency();
                await ValidateChange(s,school,request,actor.UserId,now,ct);
                var result=Guid.NewGuid();
                s.Add(new SchoolCoordination {SchoolId=school,OperationId=operation,ActorUserId=actor.UserId,Fingerprint=fingerprint,CommandJson=CoordinationWire.Save(request),BeforeRevision=row.Revision,AfterRevision=row.Revision+1,EffectiveFrom=now,ResultId=result});
                s.Add(new SchoolOperation {SchoolId=school,OperationId=operation,CommandName="Coordinate:"+request.Kind,TargetKind=request.Kind,TargetId=request.TargetId,ExpectedRevision=request.ExpectedRevision>0?request.ExpectedRevision:null,
                    CoordinationRequired=true,ExpectedSchoolRevision=row.Revision,ActorUserId=actor.UserId,Channel=OperationChannel.Web,Status=OperationStatus.Pending,RequestFingerprint=fingerprint,RequestedAt=now,AuthorityBasis=Operator(actor.UserId)?"configured platform operator":"current School authority"});
                await s.CommitAsync(ct);
            }
        }
        return await ResumeAsync(school,operation,ct);
    }
    private async Task ValidateChange(ISchoolSession s,Guid school,SchoolChangeRequest r,Guid actor,DateTimeOffset now,CancellationToken ct)
    {
        var roles=await s.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==actor,ct);
        if(r.Kind!="AcceptInvitation" && !Operator(actor) && !roles.Any(x=>x.Role==SchoolRole.Administrator && Current(x,now)))throw SchoolErrors.Missing();
        switch(r.Kind)
        {
            case "CreateEnrollment":
                if(r.StudentId is null || r.UserId is null || string.IsNullOrWhiteSpace(r.Category))throw SchoolErrors.Invalid();
                if((await s.ListAsync<StudentEnrollment>(x=>x.SchoolId==school && x.StudentId==r.StudentId && x.CategoryCode==r.Category && x.Status==EnrollmentStatus.Active,ct)).Count>0)throw SchoolErrors.Conflict("ENROLLMENT_EXISTS");break;
            case "AcceptInvitation":
                var i=await s.FindAsync<SchoolInvitation>(x=>x.SchoolId==school && x.InvitationId==r.TargetId && x.TargetUserId==actor,ct)??throw SchoolErrors.Missing();
                SchoolRules.Match(r.ExpectedRevision,i.Revision);SchoolRules.Pending(i,now);
                var issuer=await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==i.IssuedOperationId,ct)??throw SchoolErrors.Missing();
                if(!Operator(issuer.ActorUserId) && !(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==issuer.ActorUserId && x.Role==SchoolRole.Administrator,ct)).Any(x=>Current(x,now)))throw SchoolErrors.Denied();break;
            case "EndEnrollment":case "AssignGroup":case "RemoveGroup":
                var e=await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==school && x.EnrollmentId==r.TargetId,ct)??throw SchoolErrors.Missing();SchoolRules.Match(r.ExpectedRevision,e.Revision);
                if(e.Status!=EnrollmentStatus.Active)throw SchoolErrors.Conflict();
                if(r.Kind=="EndEnrollment" && r.Status=="Completed" && learning is null)throw SchoolErrors.Dependency();
                if(r.Kind=="EndEnrollment" && r.Status is not ("Completed" or "Withdrawn" or "Transferred" or "Cancelled"))throw SchoolErrors.Invalid();
                if(r.Kind=="AssignGroup" || r.Kind=="RemoveGroup")
                {
                    var g=await s.FindAsync<TrainingGroup>(x=>x.SchoolId==school && x.GroupId==r.GroupId && x.EndedAt==null,ct)??throw SchoolErrors.Missing();
                    if(g.CategoryCode!=e.CategoryCode)throw SchoolErrors.Conflict("CATEGORY_MISMATCH");
                    var assigned=await s.ListAsync<StudentGroupMembership>(x=>x.SchoolId==school && x.EnrollmentId==e.EnrollmentId && x.LeftAt==null,ct);
                    if(r.Kind=="AssignGroup" && assigned.Count>0)throw SchoolErrors.Conflict("GROUP_REASSIGNMENT_REQUIRES_REMOVAL");
                    if(r.Kind=="RemoveGroup" && !assigned.Any(x=>x.GroupId==g.GroupId))throw SchoolErrors.Missing();
                }
                if(r.Kind=="EndEnrollment" && (await s.ListAsync<StudentGroupMembership>(x=>x.SchoolId==school && x.EnrollmentId==e.EnrollmentId && x.LeftAt==null,ct)).Count>0)throw SchoolErrors.Conflict("REMOVE_GROUP_FIRST");break;
            case "EndMembership":case "GrantPermission":
                var m=await s.FindAsync<SchoolMembership>(x=>x.SchoolId==school && x.MembershipId==r.TargetId,ct)??throw SchoolErrors.Missing();
                if(!Current(m,now))throw SchoolErrors.Conflict();
                if(r.Kind=="EndMembership")SchoolRules.Match(r.ExpectedRevision,m.Revision);
                else if(!Operator(actor) || r.Permission is not ("SchoolRuleOwner" or "Assessor") || (r.Permission=="SchoolRuleOwner"?m.Role!=SchoolRole.Administrator:m.Role!=SchoolRole.Teacher))throw SchoolErrors.Denied();
                break;
            case "RevokePermission":
                if(!Operator(actor))throw SchoolErrors.Denied();
                if(await s.FindAsync<MembershipPermissionGrant>(x=>x.SchoolId==school && x.GrantId==r.TargetId && x.EndedAt==null,ct) is null)throw SchoolErrors.Missing();break;
            case "AssignTeacher":
                var group=await s.FindAsync<TrainingGroup>(x=>x.SchoolId==school && x.GroupId==r.TargetId,ct)??throw SchoolErrors.Missing();SchoolRules.Match(r.ExpectedRevision,group.Revision);
                if(r.UserId is null || !(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==r.UserId && x.Role==SchoolRole.Teacher,ct)).Any(x=>Current(x,now)))throw SchoolErrors.Denied();break;
            case "SchoolStatus":
                var schoolRow=await s.FindAsync<School>(x=>x.SchoolId==school,ct)??throw SchoolErrors.Missing();SchoolRules.Match(r.ExpectedRevision,schoolRow.Revision);
                if(r.Status is not ("Active" or "Inactive"))throw SchoolErrors.Invalid();break;
            case "BranchStatus":
                var branch=await s.FindAsync<Branch>(x=>x.SchoolId==school && x.BranchId==r.TargetId,ct)??throw SchoolErrors.Missing();SchoolRules.Match(r.ExpectedRevision,branch.Revision);
                if(r.Status is not ("Active" or "Inactive"))throw SchoolErrors.Invalid();break;
            default:throw SchoolErrors.Invalid();
        }
    }
    public async Task<SchoolChangeResponse> ResumeAsync(Guid school,Guid operation,CancellationToken ct)
    {
        if(!scheduling.Enabled)throw SchoolErrors.Gate();
        SchoolCoordination initial;
        await using(var s=await store.OpenAsync(school,false,ct))initial=await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct)??throw SchoolErrors.Missing();
        if(initial.State is "Committed" or "Aborted")return Response(initial);
        if(initial.State=="AbortPending")return await FinishAbort(school,operation,ct);
        if(initial.State=="Pending")await activity.RequireActiveAsync(initial.ActorUserId,ct);
        var command=CoordinationWire.Read<SchoolChangeRequest>(initial.CommandJson);
        if(initial.TransitionJson.Length==0)
        {
            var transition=await BuildTransition(initial,command,ct);
            await using var s=await store.OpenAsync(school,true,ct);var row=(await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
            if(row.State!="Pending")return Response(row);
            if(row.TransitionJson.Length==0){row.TransitionJson=CoordinationWire.Save(transition);row.PayloadHash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.TransitionJson)));await s.CommitAsync(ct);}
            initial=row;
        }
        if(initial.State=="Pending")
        {
            try
            {
                var prepared=await scheduling.PrepareAsync(CoordinationWire.Read<SchedulingTransition>(initial.TransitionJson),ct);
                if(prepared.OperationId!=operation || prepared.SchoolId!=school || prepared.Revision!=initial.AfterRevision || prepared.Status is not ("Prepared" or "Committed"))throw SchoolErrors.Dependency();
            }
            catch(ConflictException)
            {
                await using var rejected=await store.OpenAsync(school,true,ct);var rejectedRow=(await rejected.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
                if(rejectedRow.State=="Pending")
                {rejectedRow.State="Aborted";var op=(await rejected.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;op.Status=OperationStatus.Rejected;op.ResolvedAt=await rejected.NowAsync(ct);op.OutcomeCode="SCHEDULING_REJECTED";await rejected.CommitAsync(ct);}
                throw;
            }
            // A prepared barrier remains until authoritative local apply/abort is resolved, even if this process stops here.
            LearningCompletionEvidence? completionEvidence=null;
            if(command.Kind=="EndEnrollment" && command.Status=="Completed")
            {
                if(learning is null)throw SchoolErrors.Dependency();
                completionEvidence=await learning.RequireCurrentCompletionAsync(school,command.TargetId,ct);
            }
            await using var s=await store.OpenAsync(school,true,ct);var row=(await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
            if(row.State=="Pending")
            {
                var schoolRow=(await s.FindAsync<School>(x=>x.SchoolId==school,ct))!;
                if(schoolRow.Revision!=row.BeforeRevision)throw SchoolErrors.Conflict();
                if(completionEvidence is not null && completionEvidence.ValidUntil<=clock.GetUtcNow())throw SchoolErrors.Dependency();
                await ApplyChange(s,row,command,ct,completionEvidence);schoolRow.Revision=row.AfterRevision;schoolRow.UpdatedAt=clock.GetUtcNow();row.State="LocalApplied";
                await QueueLearningEvents(s,row,command,ct);
                var op=(await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;op.Status=OperationStatus.ReconciliationPending;op.ResultingSchoolRevision=row.AfterRevision;
                await s.CommitAsync(ct);
            }
        }
        await using(var current=await store.OpenAsync(school,false,ct))
        {
            var state=(await current.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!.State;
            if(state=="AbortPending")return await FinishAbort(school,operation,ct);
            if(state=="Aborted")return Response((await current.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!);
        }
        var finished=await scheduling.FinishAsync(school,operation,"commit",ct);
        if(finished.OperationId!=operation || finished.SchoolId!=school || finished.Status!="Committed" || finished.Revision!=initial.AfterRevision)throw SchoolErrors.Dependency();
        await using(var s=await store.OpenAsync(school,true,ct))
        {
            var row=(await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
            if(row.State=="LocalApplied")
            {row.State="Committed";var op=(await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;op.Status=OperationStatus.Committed;op.ResolvedAt=await s.NowAsync(ct);op.OutcomeCode="COMMITTED";await s.CommitAsync(ct);}
            return Response(row);
        }
    }
    private async Task<SchedulingTransition> BuildTransition(SchoolCoordination row,SchoolChangeRequest r,CancellationToken ct)
    {
        var kind=r.Kind switch {"AssignGroup" or "RemoveGroup"=>"Group","SchoolStatus"=>"School","BranchStatus"=>"Branch","EndEnrollment"=>"Enrollment","EndMembership"=>"Membership","AssignTeacher"=>"Group",_=>"Membership"};
        var target=r.GroupId??r.TargetId;var rosters=new List<SchedulingRosterChange>();string? resourceKind=null;Guid? resourceId=null;
        var reduces=r.Kind is "EndEnrollment" or "EndMembership" or "AssignTeacher" || r.Kind is "SchoolStatus" or "BranchStatus" && r.Status=="Inactive";
        if(r.Kind is "AssignGroup" or "RemoveGroup")
        {
            var impacts=await scheduling.ImpactsAsync(row.SchoolId,"Group",target,row.EffectiveFrom,ct);
            await using var s=await store.OpenAsync(row.SchoolId,false,ct);var group=(await s.FindAsync<TrainingGroup>(x=>x.SchoolId==row.SchoolId && x.GroupId==target,ct))!;
            var members=await s.ListAsync<StudentGroupMembership>(x=>x.SchoolId==row.SchoolId && x.GroupId==target,ct);
            var enrollments=await s.ListAsync<StudentEnrollment>(x=>x.SchoolId==row.SchoolId,ct);
            foreach(var lesson in impacts)
            {
                var participants=members.Where(x=>x.EnrollmentId!=r.TargetId && SchoolRules.Covers(x.JoinedAt,x.LeftAt,lesson.StartsAt,lesson.EndsAt)).Select(x=>enrollments.Single(e=>e.EnrollmentId==x.EnrollmentId)).Where(e=>SchoolRules.Covers(e.StartedAt,e.EndedAt,lesson.StartsAt,lesson.EndsAt)).Select(e=>new SchedulingParticipant(e.EnrollmentId,e.StudentId)).ToList();
                if(r.Kind=="AssignGroup"){var added=enrollments.Single(e=>e.EnrollmentId==r.TargetId);participants.Add(new(added.EnrollmentId,added.StudentId));}
                rosters.Add(new(lesson.LessonId,lesson.Version,group.MembershipRevision+1,participants.OrderBy(x=>x.StudentId).ToArray()));
            }
        }
        if(reduces)
        {
            await using var s=await store.OpenAsync(row.SchoolId,false,ct);
            if(r.Kind=="EndEnrollment"){resourceKind="Student";resourceId=(await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==row.SchoolId && x.EnrollmentId==r.TargetId,ct))!.StudentId;}
            if(r.Kind=="EndMembership")
            {
                var membership=(await s.FindAsync<SchoolMembership>(x=>x.SchoolId==row.SchoolId && x.MembershipId==r.TargetId,ct))!;
                if(r.TeacherId is Guid teacher){resourceKind="Instructor";resourceId=teacher;}
                else if(r.StudentId is Guid student){resourceKind="Student";resourceId=student;}
                else if(membership.Role!=SchoolRole.Administrator){kind="School";target=row.SchoolId;}
            }
        }
        return new(row.SchoolId,row.OperationId,row.ActorUserId,row.BeforeRevision,row.AfterRevision,kind,target,row.EffectiveFrom,reduces,resourceKind,resourceId,rosters.ToArray());
    }
    private async Task ApplyChange(ISchoolSession s,SchoolCoordination c,SchoolChangeRequest r,CancellationToken ct,LearningCompletionEvidence? completionEvidence=null)
    {
        var school=c.SchoolId;var operation=c.OperationId;var now=c.EffectiveFrom;
        async Task<SchoolMembership> EnsureMembership(Guid user,SchoolRole role)
        {
            var existing=(await s.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==user && x.Role==role,ct)).FirstOrDefault(x=>Current(x,now));
            if(existing is not null)return existing;
            var m=new SchoolMembership {MembershipId=c.ResultId,SchoolId=school,UserId=user,Role=role,StartedAt=now,Revision=1,LastOperationId=operation};s.Add(m);return m;
        }
        switch(r.Kind)
        {
            case "CreateEnrollment":
                await EnsureMembership(r.UserId!.Value,SchoolRole.Student);
                s.Add(new StudentEnrollment {EnrollmentId=c.ResultId,SchoolId=school,StudentId=r.StudentId!.Value,CategoryCode=r.Category!,Status=EnrollmentStatus.Active,StatusChangedAt=now,StartedAt=now,Revision=1,LastOperationId=operation});
                s.Add(new EnrollmentStatusHistory {SchoolId=school,EnrollmentId=c.ResultId,EnrollmentRevision=1,ToStatus=EnrollmentStatus.Active,EffectiveAt=now,RecordedAt=now,OperationId=operation});break;
            case "AcceptInvitation":
                var invitation=(await s.FindAsync<SchoolInvitation>(x=>x.SchoolId==school && x.InvitationId==r.TargetId,ct))!;
                var membership=await EnsureMembership(c.ActorUserId,invitation.Role);
                invitation.Status=InvitationStatus.Accepted;invitation.Revision++;invitation.AcceptedAt=now;invitation.AcceptedOperationId=operation;invitation.ResultMembershipId=membership.MembershipId;
                if(invitation.Role==SchoolRole.Student){s.Add(new StudentEnrollment {EnrollmentId=c.ResultId,SchoolId=school,StudentId=r.StudentId!.Value,CategoryCode=r.Category!,Status=EnrollmentStatus.Active,StatusChangedAt=now,StartedAt=now,Revision=1,LastOperationId=operation});invitation.ResultEnrollmentId=c.ResultId; s.Add(new EnrollmentStatusHistory {SchoolId=school,EnrollmentId=c.ResultId,EnrollmentRevision=1,ToStatus=EnrollmentStatus.Active,EffectiveAt=now,RecordedAt=now,OperationId=operation});}
                break;
            case "EndEnrollment":
                var e=(await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==school && x.EnrollmentId==r.TargetId,ct))!;
                if(r.Status=="Completed" && completionEvidence is null)throw SchoolErrors.Dependency();
                s.Add(new EnrollmentStatusHistory {SchoolId=school,EnrollmentId=e.EnrollmentId,EnrollmentRevision=e.Revision+1,FromStatus=e.Status,ToStatus=Enum.Parse<EnrollmentStatus>(r.Status!),EffectiveAt=now,RecordedAt=now,OperationId=operation,
                    CompletionEvidenceId=completionEvidence?.SnapshotId,CompletionDependencyRevision=completionEvidence?.DependencyRevision,CompletionVerifiedAt=completionEvidence?.VerifiedAt});
                e.Status=Enum.Parse<EnrollmentStatus>(r.Status!);e.EndedAt=now;e.StatusChangedAt=now;e.Revision++;e.LastOperationId=operation;break;
            case "EndMembership":
                var m=(await s.FindAsync<SchoolMembership>(x=>x.SchoolId==school && x.MembershipId==r.TargetId,ct))!;m.EndedAt=now;m.Revision++;m.LastOperationId=operation;
                foreach(var grant in await s.ListAsync<MembershipPermissionGrant>(x=>x.SchoolId==school && x.MembershipId==m.MembershipId && x.EndedAt==null,ct)){grant.EndedAt=now;grant.EndedOperationId=operation;}
                if(r.TeacherId is Guid endedTeacher)foreach(var assignment in await s.ListAsync<TeacherGroupAssignment>(x=>x.SchoolId==school && x.TeacherId==endedTeacher && x.EndedAt==null,ct)){assignment.EndedAt=now;assignment.Revision++;assignment.LastOperationId=operation;}
                break;
            case "AssignGroup":case "RemoveGroup":
                var enrollment=(await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==school && x.EnrollmentId==r.TargetId,ct))!;
                var group=(await s.FindAsync<TrainingGroup>(x=>x.SchoolId==school && x.GroupId==r.GroupId,ct))!;
                StudentGroupMembership? addedAssociation=null;
                if(r.Kind=="AssignGroup"){addedAssociation=new StudentGroupMembership {MembershipId=Guid.NewGuid(),SchoolId=school,EnrollmentId=enrollment.EnrollmentId,GroupId=group.GroupId,JoinedAt=now,AssociationProgramVersionId=r.ProgramVersionId!.Value,AssociationVerifiedAt=now,Revision=1,LastOperationId=operation};s.Add(addedAssociation);}
                else foreach(var association in await s.ListAsync<StudentGroupMembership>(x=>x.SchoolId==school && x.GroupId==group.GroupId && x.EnrollmentId==enrollment.EnrollmentId && x.LeftAt==null,ct)){association.LeftAt=now;association.Revision++;association.LastOperationId=operation;}
                group.MembershipRevision++;group.Revision++;group.LastOperationId=operation;enrollment.Revision++;enrollment.LastOperationId=operation;
                s.Add(new GroupRosterRevision {GroupId=group.GroupId,SchoolId=school,MembershipRevision=group.MembershipRevision,RecordedAt=now,OperationId=operation});
                var associations=await s.ListAsync<StudentGroupMembership>(x=>x.SchoolId==school && x.GroupId==group.GroupId,ct);
                var all=await s.ListAsync<StudentEnrollment>(x=>x.SchoolId==school,ct);
                var roster=associations.Where(x=>x.EnrollmentId!=enrollment.EnrollmentId && (x.LeftAt==null || x.LeftAt>now)).Select(x=>(Association:x,Enrollment:all.Single(e=>e.EnrollmentId==x.EnrollmentId))).ToList();
                if(r.Kind=="AssignGroup")roster.Add((addedAssociation!,enrollment));
                foreach(var pair in roster)s.Add(new GroupRosterMember {SchoolId=school,GroupId=group.GroupId,MembershipRevision=group.MembershipRevision,MembershipId=pair.Association.MembershipId,EnrollmentId=pair.Enrollment.EnrollmentId,EnrollmentRevision=pair.Enrollment.Revision,EnrollmentStatus=pair.Enrollment.Status,CategoryCode=pair.Enrollment.CategoryCode,StudentId=pair.Enrollment.StudentId,JoinedAt=pair.Association.JoinedAt,LeftAt=pair.Association.LeftAt,EnrollmentStartedAt=pair.Enrollment.StartedAt,EnrollmentEndedAt=pair.Enrollment.EndedAt});
                break;
            case "AssignTeacher":
                var g=(await s.FindAsync<TrainingGroup>(x=>x.SchoolId==school && x.GroupId==r.TargetId,ct))!;
                foreach(var prior in await s.ListAsync<TeacherGroupAssignment>(x=>x.SchoolId==school && x.GroupId==g.GroupId && x.EndedAt==null,ct)){prior.EndedAt=now;prior.Revision++;prior.LastOperationId=operation;}
                s.Add(new TeacherGroupAssignment {AssignmentId=c.ResultId,SchoolId=school,GroupId=g.GroupId,TeacherId=r.TeacherId!.Value,AssignedAt=now,Revision=1,LastOperationId=operation});g.Revision++;g.LastOperationId=operation;break;
            case "SchoolStatus":(await s.FindAsync<School>(x=>x.SchoolId==school,ct))!.Status=Enum.Parse<SchoolStatus>(r.Status!);break;
            case "BranchStatus":var b=(await s.FindAsync<Branch>(x=>x.SchoolId==school && x.BranchId==r.TargetId,ct))!;b.Status=Enum.Parse<SchoolStatus>(r.Status!);b.Revision++;b.LastOperationId=operation;break;
            case "GrantPermission":s.Add(new MembershipPermissionGrant {GrantId=c.ResultId,SchoolId=school,MembershipId=r.TargetId,Permission=Enum.Parse<Permission>(r.Permission!),StartedAt=now,GrantedOperationId=operation});break;
            case "RevokePermission":var p=(await s.FindAsync<MembershipPermissionGrant>(x=>x.SchoolId==school && x.GrantId==r.TargetId,ct))!;p.EndedAt=now;p.EndedOperationId=operation;break;
        }
    }
    private async Task<SchoolChangeRequest> MembershipResource(Guid school,SchoolChangeRequest request,SchoolActor actor,CancellationToken ct)
    {
        SchoolMembership member;Guid[] candidates;
        await using(var read=await store.OpenAsync(school,false,ct))
        {
            var roles=await read.ListAsync<SchoolMembership>(x=>x.SchoolId==school && x.UserId==actor.UserId,ct);
            if(!Operator(actor.UserId) && !roles.Any(x=>x.Role==SchoolRole.Administrator && Current(x,clock.GetUtcNow())))throw SchoolErrors.Missing();
            member=await read.FindAsync<SchoolMembership>(x=>x.SchoolId==school && x.MembershipId==request.TargetId,ct)??throw SchoolErrors.Missing();
            candidates=member.Role==SchoolRole.Student?(await read.ListAsync<StudentEnrollment>(x=>x.SchoolId==school,ct)).Select(x=>x.StudentId).Distinct().ToArray():
                member.Role==SchoolRole.Teacher?(await read.ListAsync<TeacherGroupAssignment>(x=>x.SchoolId==school,ct)).Select(x=>x.TeacherId).Distinct().ToArray():[];
        }
        if(member.Role==SchoolRole.Teacher)candidates=candidates.Concat((await scheduling.ImpactsAsync(school,"School",school,clock.GetUtcNow(),ct)).Select(x=>x.TeacherId)).Distinct().ToArray();
        foreach(var id in candidates)
        {
            var binding=await activity.BindingAsync(member.Role==SchoolRole.Teacher?"Teacher":"Student",id,ct);
            if(binding.UserId==member.UserId)return request with {UserId=member.UserId,TeacherId=member.Role==SchoolRole.Teacher?id:null,StudentId=member.Role==SchoolRole.Student?id:null};
        }
        // Without a retained owner binding, use the conservative whole-school future-allocation fence.
        return request with {UserId=member.UserId};
    }
    public async Task<SchoolChangeResponse> OutcomeAsync(Guid school,Guid operation,SchoolActor actor,CancellationToken ct)
    {
        SchoolRules.Fresh(actor,clock.GetUtcNow());await using var s=await store.OpenAsync(school,false,ct);
        var row=await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct)??throw SchoolErrors.Missing();
        if(row.ActorUserId!=actor.UserId && !Operator(actor.UserId))throw SchoolErrors.Missing();return Response(row);
    }
    public async Task<SchedulingDecision> DecisionAsync(Guid school,Guid operation,CancellationToken ct)
    {await using var s=await store.OpenAsync(school,false,ct);var row=await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct)??throw SchoolErrors.Missing();return new(school,operation,row.BeforeRevision,row.AfterRevision,row.PayloadHash,row.State is "LocalApplied" or "Committed",row.State=="AbortPending"?"Aborted":row.State);}
    public async Task<SchoolChangeResponse> AbortAsync(Guid school,Guid operation,string reason,SchoolActor actor,CancellationToken ct)
    {
        SchoolRules.Fresh(actor,clock.GetUtcNow());SchoolRules.Text(reason,500);
        if(!Operator(actor.UserId))throw SchoolErrors.Denied();await activity.RequireActiveAsync(actor.UserId,ct);
        await using(var s=await store.OpenAsync(school,true,ct))
        {
            var row=await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct)??throw SchoolErrors.Missing();
            if(row.State is "LocalApplied" or "Committed")throw SchoolErrors.Conflict("LOCAL_COMMIT_CANNOT_ABORT");
            if(row.State=="Aborted")return Response(row);
            if(row.State=="Pending")
            {
                var op=(await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
                var now=clock.GetUtcNow();
                s.Add(new SchoolOperation {OperationId=Guid.NewGuid(),SchoolId=school,CommandName="AbortCoordination",TargetKind="ActivationOperation",TargetId=operation,
                    ActorUserId=actor.UserId,Channel=OperationChannel.Web,Status=OperationStatus.Committed,RequestFingerprint=SchoolRules.Fingerprint("AbortCoordination",operation,new{reason},actor),
                    RequestedAt=now,ResolvedAt=now,Reason=reason,AuthorityBasis="configured platform operator",OutcomeCode="ABORT_REQUESTED"});
                if(row.TransitionJson.Length==0)
                {row.State="Aborted";op.Status=OperationStatus.Rejected;op.ResolvedAt=await s.NowAsync(ct);op.OutcomeCode="OPERATOR_ABORTED";await s.CommitAsync(ct);return Response(row);}
                row.State="AbortPending";op.Status=OperationStatus.ReconciliationPending;op.OutcomeCode="OPERATOR_ABORT_REQUESTED";await s.CommitAsync(ct);
            }
        }
        return await FinishAbort(school,operation,ct);
    }
    private async Task<SchoolChangeResponse> FinishAbort(Guid school,Guid operation,CancellationToken ct)
    {
        var result=await scheduling.FinishAsync(school,operation,"abort",ct);
        if(result.SchoolId!=school || result.OperationId!=operation || result.Status!="Aborted")throw SchoolErrors.Dependency();
        await using var s=await store.OpenAsync(school,true,ct);var row=(await s.FindAsync<SchoolCoordination>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;
        if(row.State=="AbortPending")
        {row.State="Aborted";var op=(await s.FindAsync<SchoolOperation>(x=>x.SchoolId==school && x.OperationId==operation,ct))!;op.Status=OperationStatus.Rejected;op.ResolvedAt=await s.NowAsync(ct);op.OutcomeCode="OPERATOR_ABORTED";await s.CommitAsync(ct);}
        return Response(row);
    }
}
