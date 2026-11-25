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


public sealed class UserActivity(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings) : IUserActivity
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task RequireActiveAsync(Guid id, CancellationToken ct)
    {
        SchoolRules.Id(id);
        var status = await transport.SendAsync<Status>(options.UserManagementBaseUrl, $"internal/v1/users/{id:D}/status", null, null, ct);
        if (status.UserId != id)
            throw SchoolErrors.Dependency();
        if (!status.Active)
            throw SchoolErrors.Denied();
    }
    public async Task<ProfileBinding> BindingAsync(string kind, Guid profileId, CancellationToken ct)
    {
        var binding = await transport.SendAsync<ProfileBinding>(options.UserManagementBaseUrl, $"internal/v1/profiles/{kind.ToLowerInvariant()}/{profileId:D}", null, null, ct);
        if (binding.ProfileId != profileId || binding.Kind != kind || binding.UserId == Guid.Empty)
            throw SchoolErrors.Dependency();
        return binding;
    }
    public Task<UserRoleProfiles> RoleProfilesAsync(Guid userId, CancellationToken ct) =>
        transport.SendAsync<UserRoleProfiles>(options.UserManagementBaseUrl, $"internal/v1/users/{userId:D}/role-profiles", null, null, ct);
    private sealed record Status(Guid UserId, bool Active);
}
