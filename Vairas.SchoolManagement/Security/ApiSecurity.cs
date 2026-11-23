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

namespace Vairas.SchoolManagement.Security;

public sealed class JwtOptions
{
    public string Authority { get; set; } = "https://localhost:7100";
    public string Issuer { get; set; } = "https://localhost:7100";
    public string UserAudience { get; set; } = "vairas-school-management";
    public string WorkloadAudience { get; set; } = "vairas-school-management-internal";
    // Local allowlist intersects with signed scopes; absent configuration grants nothing.
    public Dictionary<string, string[]> WorkloadClients { get; set; } = [];
}

public sealed record EntryRequirement(string? Scope = null) : IAuthorizationRequirement;

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

public static class ApiSecurity
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Authentication"));
        var settings = configuration.GetSection("Authentication").Get<JwtOptions>() ?? new();
        services.AddAuthentication("Entry")
            .AddPolicyScheme("Entry", "Route-specific bearer authentication", options =>
                options.ForwardDefaultSelector = http => http.Request.Path.StartsWithSegments("/internal") ? "Workload" : "User")
            .AddJwtBearer("User", options => Configure(options, settings, settings.UserAudience, true))
            .AddJwtBearer("Workload", options => Configure(options, settings, settings.WorkloadAudience, false));
        services.AddAuthorization(options =>
        {
            options.AddPolicy("SchoolEntry", p => p.AddRequirements(new EntryRequirement()));
            foreach (var scope in new[] { "school.profile-provisioning", "school.eligibility.read", "school.participants.read" })
                options.AddPolicy(scope, p => p.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();

        return services;
    }

    private static void Configure(JwtBearerOptions options, JwtOptions settings, string audience, bool user)
    {
        options.Authority = settings.Authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (user && context.Request.Path.StartsWithSegments("/internal"))
                {
                    var delegated = context.Request.Headers["X-User-Authorization"].ToString();
                    // A missing delegation header must never fall back to the workload bearer token.
                    if (!delegated.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        context.NoResult();
                    else
                        context.Token = delegated[7..];
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.HttpContext.WriteProblemAsync(401, "AUTHENTICATION_REQUIRED", "Authentication required.");
            },
            OnForbidden = context => context.HttpContext.WriteProblemAsync(403, "ACCESS_DENIED", "Access denied.")
        };
    }
}
