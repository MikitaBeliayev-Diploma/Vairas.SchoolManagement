using Vairas.SchoolManagement.Middleware;

namespace Vairas.SchoolManagement.Integrations;

/// <summary>Propagates the normalized current request ID, including OAuth token requests.</summary>
public sealed class DependencyCorrelationHandler(IHttpContextAccessor contexts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // Factory handlers are pooled; resolve the current context only when sending.
        // Replace stale request headers without modifying shared DefaultRequestHeaders.
        request.Headers.Remove("X-Correlation-ID");
        if (contexts.HttpContext is { } context)
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", CorrelationMiddleware.Id(context));
        return base.SendAsync(request, ct);
    }
}
