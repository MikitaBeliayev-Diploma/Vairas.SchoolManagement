using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolService
{
    public async Task<ParticipantsResponse> ParticipantsAsync(Guid school, Guid group, long? revision, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        SchoolRules.Id(school);
        SchoolRules.Id(group);
        SchoolRules.Interval(from, to);
        if (revision.HasValue)
            SchoolRules.Revision(revision);
        await using var s = await store.OpenAsync(school, false, ct);
        var g = await s.FindAsync<TrainingGroup>(x => x.SchoolId == school && x.GroupId == group, ct) ?? throw SchoolErrors.Missing();
        if (!SchoolRules.Covers(g.StartedAt, g.EndedAt, from, to))
            throw SchoolErrors.Conflict("GROUP_INTERVAL_INVALID");
        var selected = revision ?? g.MembershipRevision;
        if (!revision.HasValue)
        {
            if (await s.FindAsync<School>(x => x.SchoolId == school && x.Status == SchoolStatus.Active, ct) is null)
                throw SchoolErrors.Denied();
            if (await HasPendingCoordinationAsync(s, school, ct))
                throw SchoolErrors.Dependency();
        }
        if (await s.FindAsync<GroupRosterRevision>(x => x.SchoolId == school && x.GroupId == group && x.MembershipRevision == selected, ct) is null)
            throw SchoolErrors.Missing();
        var members = await s.ListAsync<GroupRosterMember>(x => x.SchoolId == school && x.GroupId == group && x.MembershipRevision == selected, ct);
        var eligible = members.Where(x => SchoolRules.Covers(x.JoinedAt, x.LeftAt, from, to) && SchoolRules.Covers(x.EnrollmentStartedAt, x.EnrollmentEndedAt, from, to)).ToArray();
        if (eligible.GroupBy(x => x.StudentId).Any(x => x.Count() > 1))
            throw SchoolErrors.Conflict("AMBIGUOUS_PARTICIPANT");
        return new(group, school, selected, eligible.Select(x => new ParticipantResponse(x.EnrollmentId, x.StudentId)).OrderBy(x => x.EnrollmentId).ToArray());
    }
    public async Task<EligibilityResponse> EligibilityAsync(Guid school, Guid enrollment, Guid actor, Guid teacher, CancellationToken ct)
    {
        var deadline = clock.GetUtcNow().AddSeconds(30);
        SchoolRules.Id(school);
        SchoolRules.Id(enrollment);
        SchoolRules.Id(actor);
        SchoolRules.Id(teacher);
        StudentEnrollment row;
        SchoolMembership[] roles;
        await using (var s = await store.OpenAsync(school, false, ct))
        {
            if (await s.FindAsync<School>(x => x.SchoolId == school && x.Status == SchoolStatus.Active, ct) is null)
                throw SchoolErrors.Missing();
            var now = await s.NowAsync(ct);
            row = await s.FindAsync<StudentEnrollment>(x => x.SchoolId == school && x.EnrollmentId == enrollment, ct) ?? throw SchoolErrors.Missing();
            roles = (await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor, ct)).Where(x => SchoolRules.Effective(x, now)).ToArray();
            if (roles.Length == 0 || await HasPendingCoordinationAsync(s, school, ct))
                throw SchoolErrors.Missing();
        }
        var studentBinding = await activity.BindingAsync("Student", row.StudentId, ct);
        var teacherBinding = await activity.BindingAsync("Teacher", teacher, ct);
        if (studentBinding.ProfileId != row.StudentId || studentBinding.Kind != "Student" || teacherBinding.ProfileId != teacher || teacherBinding.Kind != "Teacher" || !studentBinding.Active || !teacherBinding.Active)
            throw SchoolErrors.Denied();
        var authorized = roles.Any(x => x.Role == SchoolRole.Administrator) || roles.Any(x => x.Role == SchoolRole.Student) && actor == studentBinding.UserId || roles.Any(x => x.Role == SchoolRole.Teacher) && actor == teacherBinding.UserId;
        if (!authorized)
            throw SchoolErrors.Missing();
        await activity.RequireActiveAsync(actor, ct);
        await activity.RequireActiveAsync(studentBinding.UserId, ct);
        await activity.RequireActiveAsync(teacherBinding.UserId, ct);
        // Re-read local eligibility after external calls; no SQL lock spans RPC.
        await using var final = await store.OpenAsync(school, false, ct);
        var time = await final.NowAsync(ct);
        var current = await final.FindAsync<StudentEnrollment>(x => x.SchoolId == school && x.EnrollmentId == enrollment, ct) ?? throw SchoolErrors.Missing();
        if (current.Revision != row.Revision || await final.FindAsync<School>(x => x.SchoolId == school && x.Status == SchoolStatus.Active, ct) is null ||
         await HasPendingCoordinationAsync(final, school, ct))
            throw SchoolErrors.Dependency();
        var all = await final.ListAsync<SchoolMembership>(x => x.SchoolId == school, ct);
        if (time >= deadline)
            throw SchoolErrors.Dependency();
        var currentRoles = all.Where(x => x.UserId == actor && SchoolRules.Effective(x, time)).ToArray();
        if (!currentRoles.Any(x => x.Role == SchoolRole.Administrator) &&
         !(actor == studentBinding.UserId && currentRoles.Any(x => x.Role == SchoolRole.Student)) &&
         !(actor == teacherBinding.UserId && currentRoles.Any(x => x.Role == SchoolRole.Teacher)))
            throw SchoolErrors.Missing();
        var active = current.Status == EnrollmentStatus.Active && SchoolRules.Effective(new()
        {
            StartedAt = current.StartedAt,
            EndedAt = current.EndedAt
        }, time) &&
         all.Any(x => x.UserId == studentBinding.UserId && x.Role == SchoolRole.Student && SchoolRules.Effective(x, time));
        var allowed = all.Any(x => x.UserId == teacherBinding.UserId && x.Role == SchoolRole.Teacher && SchoolRules.Effective(x, time));
        return new(current.EnrollmentId, school, current.StudentId, active, current.CategoryCode, current.Revision, allowed);
    }
}
