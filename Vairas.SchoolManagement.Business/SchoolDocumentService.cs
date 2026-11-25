using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed class SchoolDocumentService(ISchoolStore store, IUserActivity activity, TimeProvider clock) : ISchoolDocumentService
{
    public async Task<DocumentAuthorityResponse> AuthorityAsync(DocumentAuthorityRequest request, CancellationToken ct)
    {
        SchoolRules.Id(request.SchoolId); SchoolRules.Id(request.ActorUserId); var began = clock.GetUtcNow();
        await activity.RequireActiveAsync(request.ActorUserId,ct);
        await using var s = await store.OpenAsync(request.SchoolId,false,ct); var now = await s.NowAsync(ct);
        var school = await s.FindAsync<School>(x => x.SchoolId == request.SchoolId && x.Status == SchoolStatus.Active,ct) ?? throw SchoolErrors.Missing();
        var roles = (await s.ListAsync<SchoolMembership>(x => x.SchoolId == request.SchoolId && x.UserId == request.ActorUserId,ct)).Where(x => SchoolRules.Effective(x,now)).ToArray();
        var admin = roles.Any(x => x.Role == SchoolRole.Administrator); var self = false; var assigned = false;
        StudentEnrollment? enrollment = null;
        if (request.EnrollmentId is Guid id)
        {
            enrollment = await s.FindAsync<StudentEnrollment>(x => x.SchoolId == request.SchoolId && x.EnrollmentId == id,ct) ?? throw SchoolErrors.Missing();
            if (!admin && roles.Any(x => x.Role == SchoolRole.Student))
            { var binding = await activity.BindingAsync("Student",enrollment.StudentId,ct); self = binding.Active && binding.UserId == request.ActorUserId; }
            if (!admin && roles.Any(x => x.Role == SchoolRole.Teacher) && request.TeacherId is Guid teacher)
            {
                var binding = await activity.BindingAsync("Teacher",teacher,ct);
                if (binding.Active && binding.UserId == request.ActorUserId)
                {
                    var groups = (await s.ListAsync<StudentGroupMembership>(x => x.EnrollmentId == id && x.SchoolId == request.SchoolId,ct))
                        .Where(x => x.JoinedAt <= now && (x.LeftAt is null || x.LeftAt > now)).Select(x => x.GroupId).ToArray();
                    assigned = (await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == request.SchoolId && x.TeacherId == teacher && groups.Contains(x.GroupId),ct))
                        .Any(x => x.AssignedAt <= now && (x.EndedAt is null || x.EndedAt > now));
                }
            }
        }
        // A scope-only request authorizes no enrollment or file; DM must make the exact enrollment check after resolving its local resource.
        if (!admin && !self && !assigned && !(request.EnrollmentId is null && roles.Length > 0)) throw SchoolErrors.Missing();
        if (clock.GetUtcNow() >= began.AddSeconds(30)) throw SchoolErrors.Dependency();
        return new(request.SchoolId,request.ActorUserId,request.EnrollmentId,enrollment?.StudentId,school.Name,enrollment?.CategoryCode ?? "",enrollment?.Status.ToString() ?? "",
            school.Revision,enrollment?.Revision ?? 0,admin,self,assigned,began.AddSeconds(30));
    }
}
