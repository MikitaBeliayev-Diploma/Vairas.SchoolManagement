using Microsoft.AspNetCore.Diagnostics;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Persistance;
namespace Vairas.SchoolManagement.Middleware;

/// <summary>
/// Maps application exception categories to HTTP problem responses. Provider-specific database failures are translated
/// at the persistence boundary (<see cref="DatabaseExceptionTranslator"/>) and never classified here.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        // A client that went away is not a server failure; there is nobody to answer.
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request canceled by the client");
            return true;
        }
        var (status, code, title) = exception switch
        {
            BusinessException e => (StatusOf(e), e.Code, e.Message),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.")
        };
        // Never log exception messages: database/dependency errors can contain private parameters.
        var failure = exception as PersistenceFailureException ?? exception.InnerException as PersistenceFailureException;
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning,
            "Request failed: {Code}; exception type {ExceptionType}; sqlstate {SqlState}; constraint {Constraint}",
            code, exception.GetType().Name, failure?.SqlState, failure?.Constraint);
        await http.WriteProblemAsync(status, code, title, ct, (exception as InvalidFieldsException)?.Fields);
        return true;
    }

    private static int StatusOf(BusinessException exception) => exception switch
    {
        BadRequestException or InvalidFieldsException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnprocessableEntityException => StatusCodes.Status422UnprocessableEntity,
        ServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
        GoneException => StatusCodes.Status410Gone,
        _ => StatusCodes.Status500InternalServerError
    };
}
