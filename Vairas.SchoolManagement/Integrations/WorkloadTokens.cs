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


public sealed class WorkloadTokens(IHttpClientFactory factory, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : IWorkloadTokens, IDisposable
{
    private readonly DependencyOptions options = optionsSettings.Value;
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
                throw SchoolErrors.Dependency();
            var value = await response.Content.ReadFromJsonAsync<Token>(ct);
            if (value is null || string.IsNullOrWhiteSpace(value.AccessToken) || value.TokenType != "Bearer" || value.ExpiresIn <= 30)
                throw SchoolErrors.Dependency();
            token = value.AccessToken;
            expires = clock.GetUtcNow().AddSeconds(Math.Min(value.ExpiresIn, 3600));
            return token;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw SchoolErrors.Dependency(); }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken, [property: JsonPropertyName("token_type")] string TokenType, [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
