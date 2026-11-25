using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolService
{
    public async Task<VehicleAuthorityResponse> VehicleAuthorityAsync(VehicleAuthorityRequest request, CancellationToken ct)
    {
        SchoolRules.Id(request.SchoolId);
        foreach (var id in new[] { request.BranchId, request.ActorUserId, request.TeacherId, request.SchoolMembershipId }) if (id.HasValue) SchoolRules.Id(id.Value);
        if (request.SchoolMembershipId.HasValue && !request.TeacherId.HasValue) throw SchoolErrors.Invalid();
        var deadline = clock.GetUtcNow().AddSeconds(30);
        ProfileBinding? binding = null;
        if (request.ActorUserId is Guid actor) await activity.RequireActiveAsync(actor, ct);
        if (request.TeacherId is Guid teacher)
        {
            binding = await activity.BindingAsync("Teacher", teacher, ct);
            if (binding.Kind != "Teacher" || binding.ProfileId != teacher || binding.UserId == Guid.Empty) throw SchoolErrors.Dependency();
            if (binding.Active)
            {
                try { await activity.RequireActiveAsync(binding.UserId, ct); }
                catch (ForbiddenException) { binding = binding with { Active = false }; }
            }
        }
        // All remote checks finish before the local snapshot. No School lock spans RPC.
        await using var s = await store.OpenAsync(request.SchoolId, false, ct);
        var now = await s.NowAsync(ct);
        var school = await s.FindAsync<School>(x => x.SchoolId == request.SchoolId, ct);
        if (school is null) throw SchoolErrors.Missing();
        Branch? branch = request.BranchId is Guid branchId
            ? await s.FindAsync<Branch>(x => x.SchoolId == request.SchoolId && x.BranchId == branchId, ct) : null;
        var memberships = await s.ListAsync<SchoolMembership>(x => x.SchoolId == request.SchoolId, ct);
        var administrator = request.ActorUserId is Guid actorId
            ? memberships.SingleOrDefault(x => x.UserId == actorId && x.Role == SchoolRole.Administrator && SchoolRules.Effective(x, now)) : null;
        var member = binding is null ? null : memberships.SingleOrDefault(x => x.UserId == binding.UserId && x.Role == SchoolRole.Teacher && SchoolRules.Effective(x, now) &&
            (!request.SchoolMembershipId.HasValue || x.MembershipId == request.SchoolMembershipId.Value));
        var pending = (await s.ListAsync<SchoolOperation>(x => x.SchoolId == request.SchoolId && x.CoordinationRequired &&
            (x.Status == OperationStatus.Pending || x.Status == OperationStatus.ReconciliationPending), ct)).Count > 0;
        if (pending || now >= deadline) throw SchoolErrors.Dependency();
        var allowed = school.Status == SchoolStatus.Active && (!request.BranchId.HasValue || branch?.Status == SchoolStatus.Active) &&
            (!request.ActorUserId.HasValue || administrator is not null) && (!request.TeacherId.HasValue || binding is { Active: true } && member is not null);
        var until = deadline;
        foreach (var role in new[] { administrator, member }) if (role?.EndedAt is DateTimeOffset end && end < until) until = end;
        return new(allowed, request.SchoolId, request.BranchId, request.ActorUserId, request.TeacherId,
            allowed ? member?.MembershipId : null, allowed ? binding?.UserId : null, school.Revision, branch?.Revision,
            allowed ? member?.Revision : null, until);
    }
}
