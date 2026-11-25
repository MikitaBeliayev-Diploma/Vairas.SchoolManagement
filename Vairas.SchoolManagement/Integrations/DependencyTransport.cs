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
                throw SchoolErrors.Denied();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (userToken is not null)
                    throw new UnauthorizedException("SESSION_INVALID", "The user session is invalid.");
                throw SchoolErrors.Dependency();
            }
            if (!response.IsSuccessStatusCode)
                throw SchoolErrors.Dependency();
            return await response.Content.ReadFromJsonAsync<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } }, ct) ?? throw SchoolErrors.Dependency();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw SchoolErrors.Dependency(); }
    }
}
