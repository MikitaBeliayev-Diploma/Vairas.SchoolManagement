using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolSchedulingService
{
    private async Task QueueLearningEvents(ISchoolSession s,SchoolCoordination coordination,SchoolChangeRequest request,CancellationToken ct)
    {
        var school=coordination.SchoolId;var now=clock.GetUtcNow();
        if(request.Kind is "CreateEnrollment" or "EndEnrollment" || request.Kind=="AcceptInvitation" && request.Status=="Student")
        {
            var id=request.Kind=="EndEnrollment"?request.TargetId:coordination.ResultId;
            var enrollment=request.Kind=="EndEnrollment"?await s.FindAsync<StudentEnrollment>(x=>x.SchoolId==school && x.EnrollmentId==id,ct)??throw SchoolErrors.Dependency():
                new StudentEnrollment {EnrollmentId=id,SchoolId=school,StudentId=request.StudentId!.Value,CategoryCode=request.Category!,Revision=1,Status=EnrollmentStatus.Active};
            object payload=enrollment.Status==EnrollmentStatus.Active?new{schoolId=school,enrollmentId=id,studentId=enrollment.StudentId,drivingLicenseCategory=enrollment.CategoryCode,startedAt=coordination.EffectiveFrom}:
                enrollment.Status==EnrollmentStatus.Completed?new{schoolId=school,enrollmentId=id,completedAt=coordination.EffectiveFrom}:
                new{schoolId=school,enrollmentId=id,endReason=enrollment.Status.ToString(),endedAt=coordination.EffectiveFrom};
            s.Add(new SchoolLearningOutbox {Id=Guid.NewGuid(),SchoolId=school,AggregateId=id,AggregateVersion=enrollment.Revision,
                EventType=enrollment.Status==EnrollmentStatus.Active?"StudentEnrollmentStarted":enrollment.Status==EnrollmentStatus.Completed?"StudentEnrollmentCompleted":"StudentEnrollmentEnded",
                Payload=CoordinationWire.Save(payload),
                CorrelationId=coordination.OperationId.ToString("D"),OccurredAt=now,NextAttemptAt=now});
        }
        if(request.Kind is "EndMembership" or "GrantPermission" or "RevokePermission" or "SchoolStatus" or "BranchStatus" or "AssignTeacher" or "AssignGroup" or "RemoveGroup")
            s.Add(new SchoolLearningOutbox {Id=Guid.NewGuid(),SchoolId=school,AggregateId=school,AggregateVersion=coordination.AfterRevision,EventType="SchoolLearningAuthorityChanged",
                Payload=CoordinationWire.Save(new{schoolId=school,schoolRevision=coordination.AfterRevision,changeKind=request.Kind,effectiveAt=coordination.EffectiveFrom}),CorrelationId=coordination.OperationId.ToString("D"),OccurredAt=now,NextAttemptAt=now});
    }
}
