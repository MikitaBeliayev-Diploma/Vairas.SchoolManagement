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


public interface ISchoolActorFactory
{
    Task<SchoolActor> CreateAsync(HttpContext http, CancellationToken ct);
}
