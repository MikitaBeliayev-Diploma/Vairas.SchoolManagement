using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Middleware;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Configuration.Options;
namespace Vairas.SchoolManagement.Integrations;


public sealed class SchoolActorFactory(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : ISchoolActorFactory
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task<SchoolActor> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var authenticated = await http.AuthenticateAsync("User");
        var user = authenticated.Principal;
        if (!authenticated.Succeeded || user is null ||
         !Guid.TryParse(user.FindFirstValue("sub"), out var identity) || identity == Guid.Empty ||
         !Guid.TryParse(user.FindFirstValue("sid"), out var session) || session == Guid.Empty)
            throw new UnauthorizedException("INVALID_IDENTITY", "A valid user token with a session is required.");
        var started = clock.GetUtcNow();
        var binding = await transport.SendAsync<Session>(options.AuthBaseUrl, "internal/v1/sessions/validate", new
        {
            identityId = identity,
            sessionId = session
        }, null, ct);
        if (binding.IdentityId != identity || binding.SessionId != session || binding.ValidUntil <= clock.GetUtcNow())
            throw SchoolErrors.Dependency();
        if (!binding.Active)
            throw new UnauthorizedException("SESSION_INVALID", "The session is not active.");
        var raw = http.Request.Headers.Authorization.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("INVALID_IDENTITY", "A bearer token is required.");
        var profile = Vairas.Delegation.ReadPreviewDelegation.Marked(user)
            ? await transport.SendAsync<SelfProfile>(options.UserManagementBaseUrl, "internal/v1/profiles/assistant-actor", new { identityId = identity, sessionId = session }, null, ct)
            : await transport.SendAsync<SelfProfile>(options.UserManagementBaseUrl, "api/v1/users/me", null, raw[7..], ct);
        if (profile.UserId == Guid.Empty || profile.Status != "Active")
            throw SchoolErrors.Denied();
        var until = binding.ValidUntil < started.AddSeconds(30) ? binding.ValidUntil : started.AddSeconds(30);
        var actor = new SchoolActor(profile.UserId, profile.StudentId, profile.TeacherId, profile.AdministratorId, until);
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return actor;
    }
    private sealed record Session(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
    private sealed record SelfProfile(Guid UserId, string Status, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId);
}
