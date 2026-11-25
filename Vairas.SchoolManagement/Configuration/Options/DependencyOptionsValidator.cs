using Microsoft.Extensions.Options;

namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class DependencyOptionsValidator : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions options)
    {
        if (!OriginValidation.IsExplicitHttpsOrigin(options.AuthBaseUrl) || !OriginValidation.IsExplicitHttpsOrigin(options.UserManagementBaseUrl))
            return ValidateOptionsResult.Fail("Configure explicit HTTPS dependency origins.");
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            return ValidateOptionsResult.Fail("Configure a workload client credential externally.");
        return ValidateOptionsResult.Success;
    }
}
