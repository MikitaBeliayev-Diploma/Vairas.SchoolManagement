using System.Text;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolService
{
    private async Task<ISchoolSession> InvitationScope(Guid school, SchoolActor actor, bool write, CancellationToken ct)
    {
        SchoolRules.Id(school);
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        var s = await store.OpenAsync(school, write, ct);
        try
        {
            SchoolRules.Fresh(actor, await s.NowAsync(ct));
            if (await s.FindAsync<School>(x => x.SchoolId == school, ct) is null)
                throw SchoolException.Missing();
            return s;
        }
        catch { await s.DisposeAsync(); throw; }
    }
    private async Task<bool> IssuerValid(ISchoolSession s, SchoolInvitation i, DateTimeOffset now, CancellationToken ct)
    {
        if (i.IssuerMembershipId is Guid membership)
        {
            var m = await s.FindAsync<SchoolMembership>(x => x.SchoolId == i.SchoolId && x.MembershipId == membership, ct);
            return i.Role != SchoolRole.Administrator && m is not null && m.Role == SchoolRole.Administrator && SchoolRules.Effective(m, now);
        }
        var operation = await s.FindAsync<SchoolOperation>(x => x.SchoolId == i.SchoolId && x.OperationId == i.IssuedOperationId, ct);
        return operation is not null && Operator(operation.ActorUserId);
    }
    private async Task<InvitationResponse> InvitationDto(ISchoolSession s, SchoolInvitation i, CancellationToken ct)
    {
        OperationStatus? status = null;
        if (i.AcceptedOperationId is Guid operation)
            status = (await s.FindAsync<SchoolOperation>(x => x.SchoolId == i.SchoolId && x.OperationId == operation, ct))?.Status;
        return SchoolRules.Invitation(i, status, await s.NowAsync(ct));
    }
    private async Task<bool> CanIssue(ISchoolSession s, Guid school, SchoolRole role, Guid actor, DateTimeOffset now, CancellationToken ct) =>
     Operator(actor) || role != SchoolRole.Administrator && await Admin(s, school, actor, now, ct) is not null;
    public async Task<MutationResponse<InvitationResponse>> IssueAsync(Guid school, Guid operation, IssueInvitationRequest request, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        SchoolRules.Issue(request);
        request = request with
        {
            CategoryCode = SchoolRules.Optional(request.CategoryCode, 20)
        };
        var fingerprint = SchoolRules.Fingerprint("IssueInvitation", null, request, actor);
        await using var s = await Open(school, actor, false, true, ct);
        var now = await s.NowAsync(ct);
        if (!await CanIssue(s, school, request.Role, actor.UserId, now, ct))
            throw SchoolException.Denied();
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        if (prior is not null)
            return new(operation, await InvitationDto(s, await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == prior.TargetId, ct) ?? throw SchoolException.Missing(), ct), true);
        var admin = Operator(actor.UserId) ? null : await Admin(s, school, actor.UserId, now, ct);
        var invitation = new SchoolInvitation
        {
            InvitationId = Guid.NewGuid(),
            SchoolId = school,
            TargetUserId = request.TargetUserId,
            Role = request.Role,
            CategoryCode = request.CategoryCode,
            IssuerMembershipId = admin?.MembershipId,
            Status = InvitationStatus.Pending,
            Revision = 1,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
            IssuedOperationId = operation
        };
        s.Add(Operation(school, operation, "IssueInvitation", "SchoolInvitation", invitation.InvitationId, fingerprint, actor, now, null, 1));
        s.Add(invitation);
        var response = SchoolRules.Invitation(invitation, null, now);
        await Finalize(s, actor, ct);
        return new(operation, response, false);
    }
    public async Task<InvitationResponse> InvitationAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        await using var s = await InvitationScope(school, actor, false, ct);
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id, ct) ?? throw SchoolException.Missing();
        if (i.TargetUserId != actor.UserId && !await CanIssue(s, school, i.Role, actor.UserId, await s.NowAsync(ct), ct))
            throw SchoolException.Missing();
        return await InvitationDto(s, i, ct);
    }
    public async Task<InvitationListResponse> MyInvitationsAsync(Guid? school, int limit, string? cursor, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        if (limit is < 1 or > 100)
            throw SchoolException.Invalid();
        if (school.HasValue)
            SchoolRules.Id(school.Value);
        Cursor? after = null;
        if (cursor is not null)
        {
            try
            {
                if (cursor.Length > 512)
                    throw SchoolException.Invalid();
                after = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(cursor)) ?? throw SchoolException.Invalid();
                SchoolRules.Id(after.School);
                SchoolRules.Id(after.Id);
            }
            catch (Exception e) when (e is FormatException or JsonException) { throw SchoolException.Invalid(); }
        }
        var result = new List<InvitationResponse>();
        var schools = school.HasValue ? new[] { school.Value } : await store.DiscoverAsync(actor.UserId, actor.StudentId, ct);
        foreach (var sid in schools)
        {
            await using var s = await InvitationScope(sid, actor, false, ct);
            var invitations = await s.ListAsync<SchoolInvitation>(x => x.SchoolId == sid && x.TargetUserId == actor.UserId, ct);
            var acceptedIds = invitations.Where(x => x.AcceptedOperationId.HasValue).Select(x => x.AcceptedOperationId!.Value).ToArray();
            var outcomes = acceptedIds.Length == 0 ? new Dictionary<Guid, OperationStatus>() :
             (await s.ListAsync<SchoolOperation>(x => x.SchoolId == sid && acceptedIds.Contains(x.OperationId), ct)).ToDictionary(x => x.OperationId, x => x.Status);
            var now = await s.NowAsync(ct);
            result.AddRange(invitations.Select(i => SchoolRules.Invitation(i, i.AcceptedOperationId is Guid accepted && outcomes.TryGetValue(accepted, out var status) ? status : null, now)));
        }
        var ordered = result.OrderBy(x => x.CreatedAt).ThenBy(x => x.SchoolId).ThenBy(x => x.InvitationId).Where(x => after is null || Compare(x, after) > 0).Take(limit + 1).ToArray();
        var items = ordered.Take(limit).ToArray();
        var last = items.LastOrDefault();
        var next = ordered.Length > limit && last is not null ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(last.CreatedAt, last.SchoolId, last.InvitationId))) : null;
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return new(items, next, clock.GetUtcNow());
    }
    private sealed record Cursor(DateTimeOffset Created, Guid School, Guid Id);
    private static int Compare(InvitationResponse i, Cursor c)
    {
        var v = i.CreatedAt.CompareTo(c.Created);
        if (v != 0)
            return v;
        v = i.SchoolId.CompareTo(c.School);
        return v != 0 ? v : i.InvitationId.CompareTo(c.Id);
    }
    public async Task<MutationResponse<InvitationResponse>> RevokeAsync(Guid school, Guid id, Guid operation, RevokeInvitationRequest request, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        SchoolRules.Id(operation);
        SchoolRules.Revision(request.ExpectedRevision);
        request = request with
        {
            Reason = SchoolRules.Text(request.Reason, 500)
        };
        var fingerprint = SchoolRules.Fingerprint("RevokeInvitation", id, request, actor);
        await using var s = await InvitationScope(school, actor, true, ct);
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id, ct) ?? throw SchoolException.Missing();
        if (!await CanIssue(s, school, i.Role, actor.UserId, await s.NowAsync(ct), ct))
            throw SchoolException.Missing();
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        if (prior is not null)
            return new(operation, await InvitationDto(s, i, ct), true);
        SchoolRules.Match(request.ExpectedRevision, i.Revision);
        if (i.Status != InvitationStatus.Pending)
            throw SchoolException.Conflict("INVITATION_TERMINAL");
        var now = await s.NowAsync(ct);
        i.Status = InvitationStatus.Revoked;
        i.Revision++;
        i.RevokedAt = now;
        i.RevokedOperationId = operation;
        var op = Operation(school, operation, "RevokeInvitation", "SchoolInvitation", id, fingerprint, actor, now, request.ExpectedRevision, i.Revision);
        op.Reason = request.Reason;
        s.Add(op);
        var response = SchoolRules.Invitation(i, null, now);
        await Finalize(s, actor, ct);
        return new(operation, response, false);
    }
    public async Task<AcceptanceResponse> AcceptAsync(Guid school, Guid id, Guid operation, AcceptInvitationRequest request, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        SchoolRules.Id(operation);
        SchoolRules.Revision(request.ExpectedRevision);
        var fingerprint = SchoolRules.Fingerprint("AcceptInvitation", id, request, actor);
        Guid issuer;
        // External activity calls must not be made while holding School write locks.
        await using (var read = await InvitationScope(school, actor, false, ct))
        {
            var invitation = await read.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id && x.TargetUserId == actor.UserId, ct) ?? throw SchoolException.Missing();
            var prior = await read.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == operation, ct);
            if (prior is not null)
                SchoolRules.Replay(prior, fingerprint, actor.UserId);
            if (invitation.Status == InvitationStatus.Accepted)
                return await Accepted(read, invitation, ct);
            SchoolRules.Pending(invitation, await read.NowAsync(ct));
            var issued = await read.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == invitation.IssuedOperationId, ct) ?? throw SchoolException.Missing();
            issuer = issued.ActorUserId;
        }
        await activity.RequireActiveAsync(issuer, ct);
        await using var s = await InvitationScope(school, actor, true, ct);
        var now = await s.NowAsync(ct);
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id && x.TargetUserId == actor.UserId, ct) ?? throw SchoolException.Missing();
        var existing = await Replay(s, school, operation, fingerprint, actor, ct);
        if (i.Status == InvitationStatus.Accepted)
            return await Accepted(s, i, ct);
        if (existing is not null)
            throw SchoolException.Conflict("INVITATION_TERMINAL");
        SchoolRules.Pending(i, now);
        SchoolRules.Match(request.ExpectedRevision, i.Revision);
        if (await s.FindAsync<School>(x => x.SchoolId == school && x.Status == SchoolStatus.Active, ct) is null || !await IssuerValid(s, i, now, ct))
            throw SchoolException.Denied();
        var issuedAgain = await s.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == i.IssuedOperationId, ct);
        if (issuedAgain?.ActorUserId != issuer)
            throw SchoolException.Conflict();
        if (i.Role == SchoolRole.Student)
            throw SchoolException.Gate();
        if (i.Role == SchoolRole.Teacher && actor.TeacherId is null || i.Role == SchoolRole.Administrator && actor.AdministratorId is null)
            throw new SchoolException(422, "PROFILE_REQUIRED", "Ensure your own role profile in User Management first.");
        var matching = (await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId && x.Role == i.Role, ct)).SingleOrDefault(x => SchoolRules.Effective(x, now));
        if (matching is null)
            throw SchoolException.Gate();
        // Only unchanged effective staff reuse is a local-only acceptance.
        now = await s.NowAsync(ct);
        SchoolRules.Pending(i, now);
        SchoolRules.Fresh(actor, now);
        i.Status = InvitationStatus.Accepted;
        i.Revision++;
        i.AcceptedAt = now;
        i.AcceptedOperationId = operation;
        i.ResultMembershipId = matching.MembershipId;
        s.Add(Operation(school, operation, "AcceptInvitation", "SchoolInvitation", id, fingerprint, actor, now, request.ExpectedRevision, i.Revision));
        var response = new AcceptanceResponse(operation, school, id, i.Revision, OperationStatus.Committed, "Completed", true, matching.MembershipId, null);
        await Finalize(s, actor, ct);
        return response;
    }
    private static async Task<AcceptanceResponse> Accepted(ISchoolSession s, SchoolInvitation i, CancellationToken ct)
    {
        var op = await s.FindAsync<SchoolOperation>(x => x.SchoolId == i.SchoolId && x.OperationId == i.AcceptedOperationId, ct) ?? throw new SchoolException(503, "OUTCOME_UNKNOWN", "The accepting outcome cannot be verified.");
        if (op.Status != OperationStatus.Committed)
            throw new SchoolException(503, "OUTCOME_UNKNOWN", "The accepting outcome has not been finalized.");
        return new(op.OperationId, i.SchoolId, i.InvitationId, i.Revision, op.Status, "Completed", true, i.ResultMembershipId, i.ResultEnrollmentId);
    }
    public async Task<SchoolAuthorityResponse> ProvisioningAsync(SchoolAuthorityRequest r, CancellationToken ct)
    {
        SchoolRules.Id(r.SchoolId);
        SchoolRules.Id(r.ActorUserId);
        SchoolRules.Id(r.TargetUserId);
        if (r.Role is not (SchoolRole.Teacher or SchoolRole.Administrator))
            throw SchoolException.Invalid();
        if (r.InvitationId.HasValue)
            SchoolRules.Id(r.InvitationId.Value);
        await using var s = await store.OpenAsync(r.SchoolId, false, ct);
        var now = await s.NowAsync(ct);
        var active = await s.FindAsync<School>(x => x.SchoolId == r.SchoolId && x.Status == SchoolStatus.Active, ct) is not null;
        var allowed = false;
        var basis = "denied";
        var until = now.AddSeconds(15);
        if (active && r.Bootstrap && r.Role == SchoolRole.Administrator && r.InvitationId is null && Operator(r.ActorUserId))
        {
            allowed = !(await s.ListAsync<SchoolMembership>(x => x.SchoolId == r.SchoolId && x.Role == SchoolRole.Administrator, ct)).Any(x => SchoolRules.Effective(x, now));
            if (allowed)
                basis = "configured-operator:first-administrator";
        }
        else if (active && !r.Bootstrap && r.ActorUserId == r.TargetUserId && r.InvitationId is Guid invitationId)
        {
            var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == r.SchoolId && x.InvitationId == invitationId && x.TargetUserId == r.TargetUserId && x.Role == r.Role, ct);
            if (i is not null && (i.Status == InvitationStatus.Accepted || i.Status == InvitationStatus.Pending && i.ExpiresAt > now) && await IssuerValid(s, i, now, ct))
            {
                allowed = true;
                basis = $"invitation:{i.InvitationId:D}:revision:{i.Revision}";
                if (i.Status == InvitationStatus.Pending && i.ExpiresAt < until)
                    until = i.ExpiresAt;
            }
        }
        return new(allowed, r.ActorUserId, r.TargetUserId, r.SchoolId, r.InvitationId, r.Role, r.Bootstrap, basis, until);
    }
}
