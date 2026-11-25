using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Factories for the common application errors; each returns the category exception for the intended HTTP semantics.</summary>
public static class SchoolErrors
{
    public static NotFoundException Missing() => new("NOT_FOUND", "Resource not found or not visible.");
    public static ForbiddenException Denied() => new("ACCESS_DENIED", "Access denied.");
    public static ServiceUnavailableException Dependency() => new("DEPENDENCY_UNAVAILABLE", "A required dependency is unavailable.");
    public static ServiceUnavailableException Gate() => new("ACTIVATION_INTEGRATION_NOT_READY", "Scheduling eligibility coordination is not available.");
    public static ConflictException Conflict(string code = "REVISION_CONFLICT") => new(code, "The command conflicts with current state.");
    public static BadRequestException Invalid() => new("VALIDATION_FAILED", "The request is invalid.");

    /// <summary>Rebuilds the category from a persisted outcome status (idempotent replay of a stored decision).</summary>
    public static BusinessException FromStatus(int status, string code) => status switch
    {
        400 => new BadRequestException(code), 401 => new UnauthorizedException(code), 403 => new ForbiddenException(code),
        404 => new NotFoundException(code), 409 => new ConflictException(code), 422 => new UnprocessableEntityException(code),
        503 => new ServiceUnavailableException(code), 410 => new GoneException(code),
        _ => throw new InvalidOperationException("A persisted outcome carries an unsupported status.")
    };
}
