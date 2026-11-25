using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Business;

namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class SchoolAccessOptionsValidator : IValidateOptions<SchoolAccessOptions>
{
    public ValidateOptionsResult Validate(string? name, SchoolAccessOptions options) =>
        options.PlatformOperatorUserIds.Contains(Guid.Empty)
            ? ValidateOptionsResult.Fail("Configure valid platform operator IDs externally.")
            : ValidateOptionsResult.Success;
}
