using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolService
{
    public async Task<IReadOnlyList<TeacherResourceResponse>> TeachersAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var session = await Open(school, actor, true, false, ct);
        var now = await session.NowAsync(ct);
        var members = await session.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.Role == SchoolRole.Teacher, ct);
        var result = new List<TeacherResourceResponse>();
        foreach (var member in members.Where(x => SchoolRules.Effective(x, now)))
        {
            var profile = await activity.RoleProfilesAsync(member.UserId, ct);
            if (profile.Status == "Active" && profile.TeacherId is Guid id) result.Add(new(id, member.UserId));
        }
        return result;
    }
    public async Task<IReadOnlyList<WorkspaceResponse>> WorkspacesAsync(SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        var result = new List<WorkspaceResponse>();
        foreach (var id in await store.DiscoverAsync(actor.UserId, actor.StudentId, ct))
        {
            await using var s = await store.OpenAsync(id, false, ct);
            var now = await s.NowAsync(ct);
            var memberships = (await s.ListAsync<SchoolMembership>(x => x.SchoolId == id && x.UserId == actor.UserId, ct)).Where(x => SchoolRules.Effective(x, now)).ToArray();
            var row = await s.FindAsync<School>(x => x.SchoolId == id, ct);
            if (row is null || row.Status != SchoolStatus.Active || memberships.Length == 0) continue;
            var ids = memberships.Select(x => x.MembershipId).ToArray();
            var grants = await s.ListAsync<MembershipPermissionGrant>(x => x.SchoolId == id && ids.Contains(x.MembershipId), ct);
            result.Add(new(id, row.Name, memberships.Select(x => x.Role.ToString()).Distinct().ToArray(),
                grants.Where(x => x.StartedAt <= now && (x.EndedAt == null || x.EndedAt > now)).Select(x => x.Permission.ToString()).Distinct().ToArray()));
        }
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return result.OrderBy(x => x.Name).ThenBy(x => x.SchoolId).ToArray();
    }
    public async Task<IReadOnlyList<EnrollmentResponse>> VisibleEnrollmentsAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var session = await Open(school, actor, false, false, ct);
        var rows = await session.ListAsync<StudentEnrollment>(x => x.SchoolId == school, ct);
        var result = new List<EnrollmentResponse>();
        foreach (var row in rows)
        {
            try
            {
                var visible = await EnrollmentAsync(school, row.EnrollmentId, actor, ct);
                var now = await session.NowAsync(ct);
                var assignments = await session.ListAsync<StudentGroupMembership>(x => x.SchoolId == school && x.EnrollmentId == row.EnrollmentId, ct);
                var current = assignments.Where(x => x.JoinedAt <= now && (x.LeftAt == null || x.LeftAt > now)).OrderByDescending(x => x.JoinedAt).FirstOrDefault();
                result.Add(visible with { GroupId = current?.GroupId, ProgramVersionId = current?.AssociationProgramVersionId });
            }
            catch (NotFoundException) { }
        }
        return result;
    }
}
