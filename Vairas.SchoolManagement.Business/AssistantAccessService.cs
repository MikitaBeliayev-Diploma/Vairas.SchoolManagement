using System.Security.Cryptography;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

public sealed class AssistantAccessService(ISchoolStore store, TimeProvider clock) : IAssistantAccessService
{
    public async Task<AssistantAccessResponse> ReadAsync(AssistantAccessRequest request, CancellationToken ct)
    {
        SchoolRules.Id(request.SchoolId); SchoolRules.Id(request.ActorUserId);
        await using var session = await store.OpenAsync(request.SchoolId, false, ct);
        var now = await session.NowAsync(ct);
        var school = await session.FindAsync<School>(s => s.SchoolId == request.SchoolId, ct);
        var memberships = (await session.ListAsync<SchoolMembership>(m => m.SchoolId == request.SchoolId && m.UserId == request.ActorUserId, ct))
            .Where(m => SchoolRules.Effective(m, now)).OrderBy(m => m.MembershipId).ToArray();
        var membershipIds = memberships.Select(m => m.MembershipId).ToArray();
        var grants = (await session.ListAsync<MembershipPermissionGrant>(g => g.SchoolId == request.SchoolId && membershipIds.Contains(g.MembershipId), ct))
            .Where(g => g.StartedAt <= now && (g.EndedAt is null || g.EndedAt > now)).OrderBy(g => g.GrantId).ToArray();
        var allowed = school?.Status == SchoolStatus.Active && memberships.Length > 0;
        var stamp = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1, school = request.SchoolId, actor = request.ActorUserId, school?.Status,
            memberships = memberships.Select(m => new { m.MembershipId, m.Role, m.Revision, m.StartedAt }),
            grants = grants.Select(g => new { g.GrantId, g.MembershipId, g.Permission, g.StartedAt })
        })));
        return new(request.SchoolId, request.ActorUserId, allowed, allowed && memberships.Any(m => m.Role == SchoolRole.Administrator), stamp, clock.GetUtcNow().AddSeconds(30));
    }
}
