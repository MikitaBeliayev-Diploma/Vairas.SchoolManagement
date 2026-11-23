using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Integrations;
namespace Vairas.SchoolManagement.Configuration;

public static class StartupValidation
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment, DependencyOptions dependencies, SchoolAccessOptions access)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("SchoolManagement")))
            throw new InvalidOperationException("Configure ConnectionStrings:SchoolManagement externally.");
        foreach (var value in new[] { dependencies.AuthBaseUrl, dependencies.UserManagementBaseUrl, configuration["Authentication:Authority"], configuration["Authentication:Issuer"] })
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
                throw new InvalidOperationException("Configure explicit HTTPS dependency origins and issuer.");
        if (string.IsNullOrWhiteSpace(dependencies.ClientId) || string.IsNullOrWhiteSpace(dependencies.ClientSecret) || access.PlatformOperatorUserIds.Contains(Guid.Empty))
            throw new InvalidOperationException("Configure a workload client credential and valid operator IDs externally.");
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && configuration["AllowedHosts"] is null or "" or "*")
            throw new InvalidOperationException("Configure AllowedHosts for hosting.");
    }
}
