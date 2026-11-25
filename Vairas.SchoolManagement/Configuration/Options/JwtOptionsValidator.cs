using Microsoft.Extensions.Options;

namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        OriginValidation.IsExplicitHttpsOrigin(options.Authority) && OriginValidation.IsExplicitHttpsOrigin(options.Issuer)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Configure explicit HTTPS issuer and metadata authority.");
}
