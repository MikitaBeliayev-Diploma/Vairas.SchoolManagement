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
            if (write && await HasPendingCoordinationAsync(s, school, ct)) throw SchoolErrors.Dependency();
            // Invitation access is authorized against the exact target or issuer below.
            // A self-only list must not reveal whether its school filter exists.
            return s;
        }
        catch { await s.DisposeAsync(); throw; }
    }
    private async Task<bool> IssuerValid(ISchoolSession s, SchoolInvitation i, DateTimeOffset now, CancellationToken ct)
        => await ValidIssuerAsync(s, i, now, ct) is not null;
    private async Task<Guid?> ValidIssuerAsync(ISchoolSession s, SchoolInvitation i, DateTimeOffset now, CancellationToken ct)
    {
        var operation = await s.FindAsync<SchoolOperation>(x => x.SchoolId == i.SchoolId && x.OperationId == i.IssuedOperationId, ct);
        if (operation is null || operation.ActorUserId == Guid.Empty) return null;
        if (i.IssuerMembershipId is Guid membership)
        {
            var m = await s.FindAsync<SchoolMembership>(x => x.SchoolId == i.SchoolId && x.MembershipId == membership, ct);
            return i.Role != SchoolRole.Administrator && m is not null && m.UserId == operation.ActorUserId &&
                m.Role == SchoolRole.Administrator && SchoolRules.Effective(m, now) ? operation.ActorUserId : null;
        }
        return Operator(operation.ActorUserId) ? operation.ActorUserId : null;
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
            throw SchoolErrors.Denied();
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        if (prior is not null)
            return new(operation, await InvitationDto(s, await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == prior.TargetId, ct) ?? throw SchoolErrors.Missing(), ct), true);
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
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id, ct) ?? throw SchoolErrors.Missing();
        if (i.TargetUserId != actor.UserId && !await CanIssue(s, school, i.Role, actor.UserId, await s.NowAsync(ct), ct))
            throw SchoolErrors.Missing();
        return await InvitationDto(s, i, ct);
    }
    public async Task<InvitationListResponse> SchoolInvitationsAsync(Guid school, int limit, string? cursor, SchoolActor actor, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw SchoolErrors.Invalid();
        await using var s = await Open(school, actor, true, false, ct);
        InvitationCursor? after = null;
        if (cursor is not null)
        {
            try
            {
                if (cursor.Length > 512) throw SchoolErrors.Invalid();
                after = JsonSerializer.Deserialize<InvitationCursor>(Convert.FromBase64String(cursor)) ?? throw SchoolErrors.Invalid();
                if (after.School != school) throw SchoolErrors.Invalid();
                SchoolRules.Id(after.Id);
            }
            catch (Exception e) when (e is FormatException or JsonException) { throw SchoolErrors.Invalid(); }
        }
        var rows = await s.ListSchoolInvitationsAsync(school, limit + 1, after, ct);
        var items = new List<InvitationResponse>();
        foreach (var row in rows.Take(limit)) items.Add(await InvitationDto(s, row, ct));
        var last = items.LastOrDefault();
        var next = rows.Count > limit && last is not null ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new InvitationCursor(last.CreatedAt, school, last.InvitationId))) : null;
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return new(items, next, await s.NowAsync(ct));
    }
    public async Task<InvitationListResponse> MyInvitationsAsync(Guid? school, int limit, string? cursor, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        if (limit is < 1 or > 100)
            throw SchoolErrors.Invalid();
        if (school.HasValue)
            SchoolRules.Id(school.Value);
        InvitationCursor? after = null;
        if (cursor is not null)
        {
            try
            {
                if (cursor.Length > 512)
                    throw SchoolErrors.Invalid();
                after = JsonSerializer.Deserialize<InvitationCursor>(Convert.FromBase64String(cursor)) ?? throw SchoolErrors.Invalid();
                SchoolRules.Id(after.School);
                SchoolRules.Id(after.Id);
            }
            catch (Exception e) when (e is FormatException or JsonException) { throw SchoolErrors.Invalid(); }
        }
        var result = new List<InvitationResponse>();
        var schools = school.HasValue ? new[] { school.Value } : await store.DiscoverAsync(actor.UserId, actor.StudentId, ct);
        foreach (var sid in schools)
        {
            await using var s = await InvitationScope(sid, actor, false, ct);
            // Each scoped query returns at most one page plus a sentinel. No school can
            // contribute a later row to the global page before its own first limit+1 rows.
            var invitations = await s.ListOwnInvitationsAsync(sid, actor.UserId, limit + 1, after, ct);
            var acceptedIds = invitations.Where(x => x.AcceptedOperationId.HasValue).Select(x => x.AcceptedOperationId!.Value).ToArray();
            var outcomes = acceptedIds.Length == 0 ? new Dictionary<Guid, OperationStatus>() :
             (await s.ListAsync<SchoolOperation>(x => x.SchoolId == sid && acceptedIds.Contains(x.OperationId), ct)).ToDictionary(x => x.OperationId, x => x.Status);
            var now = await s.NowAsync(ct);
            result.AddRange(invitations.Select(i => SchoolRules.Invitation(i, i.AcceptedOperationId is Guid accepted && outcomes.TryGetValue(accepted, out var status) ? status : null, now)));
        }
        var ordered = result.OrderBy(x => x.CreatedAt).ThenBy(x => x.SchoolId).ThenBy(x => x.InvitationId).Take(limit + 1).ToArray();
        var items = ordered.Take(limit).ToArray();
        var last = items.LastOrDefault();
        var next = ordered.Length > limit && last is not null ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new InvitationCursor(last.CreatedAt, last.SchoolId, last.InvitationId))) : null;
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return new(items, next, clock.GetUtcNow());
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
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id, ct) ?? throw SchoolErrors.Missing();
        if (!await CanIssue(s, school, i.Role, actor.UserId, await s.NowAsync(ct), ct))
            throw SchoolErrors.Missing();
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        if (prior is not null)
            return new(operation, await InvitationDto(s, i, ct), true);
        SchoolRules.Match(request.ExpectedRevision, i.Revision);
        if (i.Status != InvitationStatus.Pending)
            throw SchoolErrors.Conflict("INVITATION_TERMINAL");
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
        if(coordination is not null && schedulingClient?.Enabled==true)
        {
            var result=await coordination.ChangeAsync(school,operation,new("AcceptInvitation",id,request.ExpectedRevision),actor,ct);
            await using var completed=await store.OpenAsync(school,false,ct);
            var invitation=await completed.FindAsync<SchoolInvitation>(x=>x.SchoolId==school && x.InvitationId==id,ct)??throw SchoolErrors.Missing();
            return new(operation,school,id,invitation.Revision,result.State=="Committed"?OperationStatus.Committed:OperationStatus.ReconciliationPending,result.State,result.State=="Committed",invitation.ResultMembershipId,invitation.ResultEnrollmentId);
        }
        var fingerprint = SchoolRules.Fingerprint("AcceptInvitation", id, request, actor);
        Guid issuer;
        // External activity calls must not be made while holding School write locks.
        await using (var read = await InvitationScope(school, actor, false, ct))
        {
            var invitation = await read.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id && x.TargetUserId == actor.UserId, ct) ?? throw SchoolErrors.Missing();
            var prior = await read.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == operation, ct);
            if (prior is not null)
                SchoolRules.Replay(prior, fingerprint, actor.UserId);
            if (invitation.Status == InvitationStatus.Accepted)
                return await Accepted(read, invitation, ct);
            SchoolRules.Pending(invitation, await read.NowAsync(ct));
            var issued = await read.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == invitation.IssuedOperationId, ct) ?? throw SchoolErrors.Missing();
            issuer = issued.ActorUserId;
        }
        await activity.RequireActiveAsync(issuer, ct);
        await using var s = await InvitationScope(school, actor, true, ct);
        var now = await s.NowAsync(ct);
        var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == school && x.InvitationId == id && x.TargetUserId == actor.UserId, ct) ?? throw SchoolErrors.Missing();
        var existing = await Replay(s, school, operation, fingerprint, actor, ct);
        if (i.Status == InvitationStatus.Accepted)
            return await Accepted(s, i, ct);
        if (existing is not null)
            throw SchoolErrors.Conflict("INVITATION_TERMINAL");
        SchoolRules.Pending(i, now);
        SchoolRules.Match(request.ExpectedRevision, i.Revision);
        if (await s.FindAsync<School>(x => x.SchoolId == school && x.Status == SchoolStatus.Active, ct) is null || !await IssuerValid(s, i, now, ct))
            throw SchoolErrors.Denied();
        var issuedAgain = await s.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == i.IssuedOperationId, ct);
        if (issuedAgain?.ActorUserId != issuer)
            throw SchoolErrors.Conflict();
        if (i.Role == SchoolRole.Student)
            throw SchoolErrors.Gate();
        if (i.Role == SchoolRole.Teacher && actor.TeacherId is null || i.Role == SchoolRole.Administrator && actor.AdministratorId is null)
            throw new UnprocessableEntityException("PROFILE_REQUIRED", "Ensure your own role profile in User Management first.");
        var matching = (await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId && x.Role == i.Role, ct)).SingleOrDefault(x => SchoolRules.Effective(x, now));
        if (matching is null)
            throw SchoolErrors.Gate();
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
        var op = await s.FindAsync<SchoolOperation>(x => x.SchoolId == i.SchoolId && x.OperationId == i.AcceptedOperationId, ct) ?? throw new ServiceUnavailableException("OUTCOME_UNKNOWN", "The accepting outcome cannot be verified.");
        if (op.Status != OperationStatus.Committed)
            throw new ServiceUnavailableException("OUTCOME_UNKNOWN", "The accepting outcome has not been finalized.");
        return new(op.OperationId, i.SchoolId, i.InvitationId, i.Revision, op.Status, "Completed", true, i.ResultMembershipId, i.ResultEnrollmentId);
    }
    public async Task<SchoolAuthorityResponse> ProvisioningAsync(SchoolAuthorityRequest r, CancellationToken ct)
    {
        SchoolRules.Id(r.SchoolId);
        SchoolRules.Id(r.ActorUserId);
        SchoolRules.Id(r.TargetUserId);
        if (r.Role is not (SchoolRole.Teacher or SchoolRole.Administrator))
            throw SchoolErrors.Invalid();
        if (r.InvitationId.HasValue)
            SchoolRules.Id(r.InvitationId.Value);
        await using var s = await store.OpenAsync(r.SchoolId, false, ct);
        var now = await s.NowAsync(ct);
        var active = await s.FindAsync<School>(x => x.SchoolId == r.SchoolId && x.Status == SchoolStatus.Active, ct) is not null;
        var allowed = false;
        Guid? issuerUserId = null;
        var basis = "denied";
        var until = now.AddSeconds(15);
        if (active && r.Bootstrap && r.Role == SchoolRole.Administrator && r.InvitationId is null && Operator(r.ActorUserId))
        {
            allowed = !(await s.ListAsync<SchoolMembership>(x => x.SchoolId == r.SchoolId && x.Role == SchoolRole.Administrator, ct)).Any(x => SchoolRules.Effective(x, now));
            if (allowed)
            {
                basis = "configured-operator:first-administrator";
                issuerUserId = r.ActorUserId;
            }
        }
        else if (active && !r.Bootstrap && r.ActorUserId == r.TargetUserId && r.InvitationId is Guid invitationId)
        {
            var i = await s.FindAsync<SchoolInvitation>(x => x.SchoolId == r.SchoolId && x.InvitationId == invitationId && x.TargetUserId == r.TargetUserId && x.Role == r.Role, ct);
            if (i is not null && (i.Status == InvitationStatus.Accepted || i.Status == InvitationStatus.Pending && i.ExpiresAt > now) &&
                await ValidIssuerAsync(s, i, now, ct) is Guid issuer)
            {
                allowed = true;
                issuerUserId = issuer;
                basis = $"invitation:{i.InvitationId:D}:revision:{i.Revision}";
                if (i.Status == InvitationStatus.Pending && i.ExpiresAt < until)
                    until = i.ExpiresAt;
            }
        }
        return new(allowed, r.ActorUserId, r.TargetUserId, r.SchoolId, r.InvitationId, r.Role, r.Bootstrap, basis, until, issuerUserId);
    }
}

