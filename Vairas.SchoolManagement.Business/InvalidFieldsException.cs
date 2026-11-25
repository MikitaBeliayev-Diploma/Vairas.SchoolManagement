namespace Vairas.SchoolManagement.Business;

/// <summary>
/// The request fails validation and the invalid request fields can be named safely (US-001 / AC-002). Field names are request
/// property names only; values and foreign data are never included. The public code stays <c>VALIDATION_FAILED</c>.
/// </summary>
public sealed class InvalidFieldsException(IReadOnlyList<string> fields)
    : BusinessException("VALIDATION_FAILED", "The request is invalid.")
{
    public IReadOnlyList<string> Fields { get; } = fields;
}
