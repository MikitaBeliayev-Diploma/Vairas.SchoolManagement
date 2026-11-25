using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed class SchoolLearningService(ISchoolStore store, IUserActivity activity, TimeProvider clock) : ISchoolLearningService
{
    public async Task<LearningAuthorityResponse> AuthorityAsync(LearningAuthorityRequest request, CancellationToken ct)
    {
        SchoolRules.Id(request.SchoolId); var began = clock.GetUtcNow();
        await using var s = await store.OpenAsync(request.SchoolId, false, ct);
        var school = await s.FindAsync<School>(x => x.SchoolId == request.SchoolId && x.Status == SchoolStatus.Active, ct) ?? throw SchoolErrors.Missing();
        var now = await s.NowAsync(ct);
        var roles = request.ActorUserId == Guid.Empty ? [] : (await s.ListAsync<SchoolMembership>(x => x.SchoolId == request.SchoolId && x.UserId == request.ActorUserId, ct)).Where(x => SchoolRules.Effective(x, now)).ToArray();
        var admin = roles.Any(x => x.Role == SchoolRole.Administrator); var teacher = roles.Any(x => x.Role == SchoolRole.Teacher);
        var ids = roles.Where(x => x.Role == SchoolRole.Teacher).Select(x => x.MembershipId).ToArray();
        var grants = await s.ListAsync<MembershipPermissionGrant>(x => x.SchoolId == request.SchoolId && ids.Contains(x.MembershipId) && x.Permission == Permission.Assessor, ct);
        var assessor = teacher && grants.Any(x => x.StartedAt <= now && (x.EndedAt is null || x.EndedAt > now));
        StudentEnrollment? enrollment = null; StudentGroupMembership? association = null; TrainingGroup? group = null;
        if (request.EnrollmentId is Guid enrollmentId)
        {
            enrollment = await s.FindAsync<StudentEnrollment>(x => x.SchoolId == request.SchoolId && x.EnrollmentId == enrollmentId, ct) ?? throw SchoolErrors.Missing();
            var groups = (await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == request.SchoolId && x.EnrollmentId == enrollmentId, ct)).Where(x => x.JoinedAt <= now && (x.LeftAt is null || x.LeftAt > now)).ToArray();
            if (groups.Length > 1) throw SchoolErrors.Conflict("AMBIGUOUS_PROGRAMME_ASSOCIATION");
            association = groups.SingleOrDefault();
        }
        var groupId = request.GroupId ?? association?.GroupId;
        if (groupId is Guid g)
        {
            group = await s.FindAsync<TrainingGroup>(x => x.SchoolId == request.SchoolId && x.GroupId == g, ct) ?? throw SchoolErrors.Missing();
            if (enrollment is not null && group.CategoryCode != enrollment.CategoryCode) throw SchoolErrors.Conflict("GROUP_CATEGORY_MISMATCH");
        }
        var assigned = false;
        if (!admin && teacher && request.TeacherId is Guid teacherId && groupId is Guid assignedGroup)
        {
            // The teacher profile must belong to the caller (same rule as document authority); a borrowed TeacherId never admits.
            var binding = await activity.BindingAsync("Teacher", teacherId, ct);
            if (binding.Active && binding.UserId == request.ActorUserId)
                assigned = (await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == request.SchoolId && x.GroupId == assignedGroup && x.TeacherId == teacherId, ct)).Any(x => x.AssignedAt <= now && (x.EndedAt is null || x.EndedAt > now));
        }
        var self = false;
        if (!admin && enrollment is not null && roles.Any(x => x.Role == SchoolRole.Student))
        {
            var binding = await activity.BindingAsync("Student", enrollment.StudentId, ct);
            self = binding.Active && binding.UserId == request.ActorUserId;
        }
        var integration = request.ActorUserId == Guid.Empty;
        var allowed = integration || admin || self || teacher && (assessor || assigned || enrollment is null && groupId is null);
        if (!allowed) throw SchoolErrors.Missing();
        // Evidence-only validation is a read-only owner leaf used while School completes its own coordination.
        // New Learning commands always request ordinary admission and remain blocked by pending coordination.
        if (!integration && !request.EvidenceRevalidation && (await s.ListAsync<SchoolOperation>(x => x.SchoolId == request.SchoolId && x.CoordinationRequired && (x.Status == OperationStatus.Pending || x.Status == OperationStatus.ReconciliationPending), ct)).Count > 0) throw SchoolErrors.Dependency();
        if (clock.GetUtcNow() >= began.AddSeconds(30)) throw SchoolErrors.Dependency();
        var latest = await s.FindAsync<SchoolCoordination>(x => x.SchoolId == request.SchoolId && x.AfterRevision == school.Revision && (x.State == "LocalApplied" || x.State == "Committed"), ct);
        return new(request.SchoolId, request.ActorUserId, request.EnrollmentId, enrollment?.StudentId, enrollment?.CategoryCode ?? group?.CategoryCode ?? "", enrollment?.Status.ToString() ?? "",
            school.Revision, enrollment?.Revision ?? 0, true, admin, teacher, assessor, self, assigned, groupId, association?.GroupId == groupId ? association?.AssociationProgramVersionId : null,
            group?.BranchId, began.AddSeconds(30), integration ? "authorized Learning workload metadata" : $"School revision {school.Revision}; current membership/Assessor grants",
            latest?.EffectiveFrom, latest is null ? null : "ProspectiveOnly");
    }
    public async Task<LearningPlanningResourcesResponse> ResourcesAsync(LearningPlanningResourcesRequest request, CancellationToken ct)
    {
        var authority = await AuthorityAsync(new(request.SchoolId, request.ActorUserId, request.EnrollmentId, request.TeacherId, request.GroupId), ct);
        if (!authority.Administrator || authority.GroupId is not Guid groupId || authority.BranchId is not Guid branch || request.EnrollmentId is not null && authority.EnrollmentStatus != "Active") throw SchoolErrors.Denied();
        await using var s = await store.OpenAsync(request.SchoolId, false, ct); var now = await s.NowAsync(ct);
        var assignments = await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == request.SchoolId && x.GroupId == groupId, ct);
        var teachers = assignments.Where(x => x.AssignedAt <= now && (x.EndedAt is null || x.EndedAt > now)).Select(x => x.TeacherId).Distinct().Order().ToArray();
        LearningParticipant[] participants;
        if (request.EnrollmentId is Guid enrollment) participants = [new(enrollment, authority.StudentId!.Value)];
        else
        {
            var associations = await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == request.SchoolId && x.GroupId == groupId, ct);
            var ids = associations.Where(x => x.JoinedAt <= now && (x.LeftAt is null || x.LeftAt > now)).Select(x => x.EnrollmentId).ToArray();
            participants = (await s.ListAsync<StudentEnrollment>(x => x.SchoolId == request.SchoolId && ids.Contains(x.EnrollmentId) && x.Status == EnrollmentStatus.Active, ct)).Select(x => new LearningParticipant(x.EnrollmentId, x.StudentId)).OrderBy(x => x.EnrollmentId).ToArray();
        }
        if (participants.Length == 0 || participants.Select(x => x.StudentId).Distinct().Count() != participants.Length || teachers.Length == 0) throw SchoolErrors.Conflict("PLANNING_RESOURCES_INCOMPLETE");
        return new(request.SchoolId, authority.Category, branch, teachers, participants, authority.ValidUntil);
    }
}
