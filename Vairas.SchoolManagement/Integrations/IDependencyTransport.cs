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


public interface IDependencyTransport
{
    Task<T> SendAsync<T>(string origin, string route, object? body, string? userToken, CancellationToken ct);
}
