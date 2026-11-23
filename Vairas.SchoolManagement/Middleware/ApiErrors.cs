using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vairas.SchoolManagement.Business;
namespace Vairas.SchoolManagement.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, code, title) = exception switch
        {
            SchoolException e => (e.Status, e.Code, e.Message),
            DbUpdateConcurrencyException => (409, "REVISION_CONFLICT", "The resource has changed."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23P01" } } => (409, "RELATIONSHIP_CONFLICT", "The command conflicts with existing state."),
            DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23514" } } => (409, "BUSINESS_RULE_REJECTED", "The command violates a business rule."),
            PostgresException { SqlState: "55P03" or "40001" or "40P01" } => (503, "RETRY_REQUIRED", "Retry the same operation."),
            NpgsqlException => (503, "DATABASE_UNAVAILABLE", "The database is unavailable."),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.")
        };
        // Never log exception messages: database/dependency errors can contain private parameters.
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Warning, "Request failed: {Code}; exception type {ExceptionType}", code, exception.GetType().Name);
        await http.WriteProblemAsync(status, code, title, ct);
        return true;
    }
}
public static class Problems
{
    public static async Task WriteProblemAsync(this HttpContext http, int status, string code, string title, CancellationToken ct = default)
    {
        http.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Instance = http.Request.Path };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = CorrelationMiddleware.Id(http);
        await http.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
    }
}
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public static string Id(HttpContext http) => (string?)http.Items["CorrelationId"] ?? http.TraceIdentifier;
    public async Task InvokeAsync(HttpContext http, ILogger<CorrelationMiddleware> logger)
    {
        var supplied = http.Request.Headers["X-Correlation-ID"].ToString();
        var id = supplied.Length is > 0 and <= 128 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? supplied : Guid.NewGuid().ToString("D");
        http.Items["CorrelationId"] = id;
        http.Response.Headers["X-Correlation-ID"] = id;
        http.Response.Headers.CacheControl = "no-store";
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
            await next(http);
    }
}
