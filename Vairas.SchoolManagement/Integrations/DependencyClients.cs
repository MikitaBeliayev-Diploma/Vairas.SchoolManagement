using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Middleware;
namespace Vairas.SchoolManagement.Integrations;

public sealed class DependencyOptions
{
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string UserManagementBaseUrl { get; set; } = "https://localhost:7001/";
    public string ClientId { get; set; } = "school-management";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "identity.validate profiles.status.read profiles.binding.read";
}
public interface IWorkloadTokens
{
    Task<string> GetAsync(CancellationToken ct);
}
public sealed class WorkloadTokens(IHttpClientFactory factory, DependencyOptions options, TimeProvider clock) : IWorkloadTokens, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1); private string? token; private DateTimeOffset expires;
    public async Task<string> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (token is not null && expires > clock.GetUtcNow().AddSeconds(30))
                return token;
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.AuthBaseUrl), "connect/token"))
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret, ["scope"] = options.Scope })
            };
            using var response = await factory.CreateClient("dependencies").SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw SchoolException.Dependency();
            var value = await response.Content.ReadFromJsonAsync<Token>(ct);
            if (value is null || string.IsNullOrWhiteSpace(value.AccessToken) || value.TokenType != "Bearer" || value.ExpiresIn <= 30)
                throw SchoolException.Dependency();
            token = value.AccessToken;
            expires = clock.GetUtcNow().AddSeconds(Math.Min(value.ExpiresIn, 3600));
            return token;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw SchoolException.Dependency(); }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("token_type")] string TokenType, [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
public interface IDependencyTransport
{
    Task<T> SendAsync<T>(string origin, string route, object? body, string? userToken, CancellationToken ct);
}
public sealed class DependencyTransport(IHttpClientFactory factory, IWorkloadTokens tokens, IHttpContextAccessor accessor) : IDependencyTransport
{
    public async Task<T> SendAsync<T>(string origin, string route, object? body, string? userToken, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(new Uri(origin), route));
            if (body is not null)
                request.Content = JsonContent.Create(body);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken ?? await tokens.GetAsync(ct));
            if (accessor.HttpContext is HttpContext http)
                request.Headers.Add("X-Correlation-ID", CorrelationMiddleware.Id(http));
            using var response = await factory.CreateClient("dependencies").SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                throw SchoolException.Denied();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (userToken is not null)
                    throw new SchoolException(401, "SESSION_INVALID", "The user session is invalid.");
                throw SchoolException.Dependency();
            }
            if (!response.IsSuccessStatusCode)
                throw SchoolException.Dependency();
            return await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }, ct) ?? throw SchoolException.Dependency();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw SchoolException.Dependency(); }
    }
}
public sealed class UserActivity(IDependencyTransport transport, DependencyOptions options) : IUserActivity
{
    public async Task RequireActiveAsync(Guid id, CancellationToken ct)
    {
        SchoolRules.Id(id);
        var status = await transport.SendAsync<Status>(options.UserManagementBaseUrl, $"internal/v1/users/{id:D}/status", null, null, ct);
        if (status.UserId != id)
            throw SchoolException.Dependency();
        if (!status.Active)
            throw SchoolException.Denied();
    }
    public async Task<ProfileBinding> BindingAsync(string kind, Guid profileId, CancellationToken ct)
    {
        var binding = await transport.SendAsync<ProfileBinding>(options.UserManagementBaseUrl, $"internal/v1/profiles/{kind.ToLowerInvariant()}/{profileId:D}", null, null, ct);
        if (binding.ProfileId != profileId || binding.Kind != kind || binding.UserId == Guid.Empty)
            throw SchoolException.Dependency();
        return binding;
    }
    private sealed record Status(Guid UserId, bool Active);
}
public interface ISchoolActorFactory
{
    Task<SchoolActor> CreateAsync(HttpContext http, CancellationToken ct);
}
public sealed class SchoolActorFactory(IDependencyTransport transport, DependencyOptions options, TimeProvider clock) : ISchoolActorFactory
{
    public async Task<SchoolActor> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var authenticated = await http.AuthenticateAsync("User");
        var user = authenticated.Principal;
        if (!authenticated.Succeeded || user is null ||
         !Guid.TryParse(user.FindFirstValue("sub"), out var identity) || identity == Guid.Empty ||
         !Guid.TryParse(user.FindFirstValue("sid"), out var session) || session == Guid.Empty)
            throw new SchoolException(401, "INVALID_IDENTITY", "A valid user token with a session is required.");
        var started = clock.GetUtcNow();
        var binding = await transport.SendAsync<Session>(options.AuthBaseUrl, "internal/v1/sessions/validate", new
        {
            identityId = identity,
            sessionId = session
        }, null, ct);
        if (binding.IdentityId != identity || binding.SessionId != session || binding.ValidUntil <= clock.GetUtcNow())
            throw SchoolException.Dependency();
        if (!binding.Active)
            throw new SchoolException(401, "SESSION_INVALID", "The session is not active.");
        var raw = http.Request.Headers.Authorization.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new SchoolException(401, "INVALID_IDENTITY", "A bearer token is required.");
        var profile = await transport.SendAsync<SelfProfile>(options.UserManagementBaseUrl, "api/v1/users/me", null, raw[7..], ct);
        if (profile.UserId == Guid.Empty || profile.Status != "Active")
            throw SchoolException.Denied();
        var until = binding.ValidUntil < started.AddSeconds(30) ? binding.ValidUntil : started.AddSeconds(30);
        var actor = new SchoolActor(profile.UserId, profile.StudentId, profile.TeacherId, profile.AdministratorId, until);
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return actor;
    }
    private sealed record Session(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
    private sealed record SelfProfile(Guid UserId, string Status, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId);
}
