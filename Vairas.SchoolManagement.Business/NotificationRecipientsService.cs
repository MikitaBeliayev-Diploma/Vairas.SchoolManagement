using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;
public sealed class NotificationRecipientsService(ISchoolStore store, IUserActivity activity, TimeProvider clock) : INotificationRecipientsService
{
    public async Task<NotificationRecipientsResponse> ResolveAsync(NotificationRecipientsRequest request, CancellationToken ct)
    {
        SchoolRules.Id(request.SchoolId);
        if (request.EnrollmentIds is null || request.EnrollmentIds.Length > 500 || request.EnrollmentIds.Any(x => x == Guid.Empty)
            || request.Kind is not ("Lesson" or "Assessment") || request.TeacherId == Guid.Empty) throw SchoolErrors.Invalid();
        var started = clock.GetUtcNow();
        School school;
        Guid[] studentIds;
        await using (var session = await store.OpenAsync(request.SchoolId, false, ct))
        {
            school = await session.FindAsync<School>(x => x.SchoolId == request.SchoolId, ct) ?? throw SchoolErrors.Missing();
            if (school.Status != SchoolStatus.Active) return new(request.SchoolId, school.Name, school.Revision, [], started.AddSeconds(30));
            if ((await session.ListAsync<SchoolOperation>(x => x.SchoolId == request.SchoolId && x.CoordinationRequired
                && (x.Status == OperationStatus.Pending || x.Status == OperationStatus.ReconciliationPending), ct)).Count != 0) throw SchoolErrors.Dependency();
            var now = await session.NowAsync(ct);
            studentIds = (await session.ListAsync<StudentEnrollment>(x => x.SchoolId == request.SchoolId && request.EnrollmentIds.Contains(x.EnrollmentId), ct))
                .Where(x => x.StartedAt <= now && (x.Status == EnrollmentStatus.Active && (x.EndedAt is null || x.EndedAt > now)
                    || request.Kind == "Assessment" && x.Status == EnrollmentStatus.Completed)).Select(x => x.StudentId).Distinct().ToArray();
        }
        // No SQL transaction spans the remote User Management bindings.
        var bindings = new List<(ProfileBinding Binding, SchoolRole Role)>();
        foreach (var batch in studentIds.Chunk(16))
            bindings.AddRange((await Task.WhenAll(batch.Select(id => activity.BindingAsync("Student", id, ct)))).Select(x => (x, SchoolRole.Student)));
        if (request.Kind == "Lesson" && request.TeacherId is Guid teacher)
            bindings.Add((await activity.BindingAsync("Teacher", teacher, ct), SchoolRole.Teacher));
        await using var verify = await store.OpenAsync(request.SchoolId, false, ct);
        var current = await verify.FindAsync<School>(x => x.SchoolId == request.SchoolId, ct) ?? throw SchoolErrors.Missing();
        if (current.Status != SchoolStatus.Active || current.Revision != school.Revision || clock.GetUtcNow() >= started.AddSeconds(30)) throw SchoolErrors.Dependency();
        var time = await verify.NowAsync(ct);
        var memberships = await verify.ListAsync<SchoolMembership>(x => x.SchoolId == request.SchoolId, ct);
        var users = bindings.Where(x => x.Binding.Active && x.Binding.UserId != Guid.Empty && memberships.Any(m => m.UserId == x.Binding.UserId
            && m.Role == x.Role && SchoolRules.Effective(m, time))).Select(x => x.Binding.UserId).Distinct().Order().ToArray();
        return new(request.SchoolId, current.Name, current.Revision, users, started.AddSeconds(30));
    }
}
