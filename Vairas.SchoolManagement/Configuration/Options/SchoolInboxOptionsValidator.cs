using Microsoft.Extensions.Options;

namespace Vairas.SchoolManagement.Configuration.Options;

/// <summary>A disabled inbox requires no broker credentials.</summary>
public sealed class SchoolInboxOptionsValidator : IValidateOptions<SchoolInboxOptions>
{
    public ValidateOptionsResult Validate(string? name, SchoolInboxOptions options) =>
        options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail("Enabled inbox requires broker credentials.")
            : ValidateOptionsResult.Success;
}
