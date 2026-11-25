using Microsoft.Extensions.Options;

namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options) =>
        string.IsNullOrWhiteSpace(options.SchoolManagement)
            ? ValidateOptionsResult.Fail("Configure ConnectionStrings:SchoolManagement externally.")
            : ValidateOptionsResult.Success;
}
