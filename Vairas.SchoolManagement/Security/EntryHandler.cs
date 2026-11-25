using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Middleware;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Configuration.Options;

namespace Vairas.SchoolManagement.Security;


public sealed class EntryHandler(IOptions<JwtOptions> settings) : AuthorizationHandler<EntryRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EntryRequirement requirement)
    {
        if (context.Resource is not HttpContext http)
            return;
        if (!http.Request.Path.StartsWithSegments("/internal"))
        {
            if ((await http.AuthenticateAsync("User")).Succeeded)
                context.Succeed(requirement);
            return;
        }
        var result = await http.AuthenticateAsync("Workload");
        if (!result.Succeeded || result.Principal is null)
            return;
        var client = result.Principal.FindFirstValue("sub");
        var required = requirement.Scope ?? "school.eligibility.read";
        var scopes = result.Principal.FindAll("scope").SelectMany(x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (client is not null && settings.Value.WorkloadClients.TryGetValue(client, out var allowed) &&
            allowed.Contains(required, StringComparer.Ordinal) && scopes.Contains(required, StringComparer.Ordinal))
            context.Succeed(requirement);
    }
}
