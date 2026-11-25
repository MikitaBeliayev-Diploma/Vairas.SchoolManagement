using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Vairas.SchoolManagement.Integrations;
using Vairas.SchoolManagement.Middleware;

namespace Vairas.SchoolManagement.Tests;

internal sealed class CorrelationCapture : HttpMessageHandler
{
    public ConcurrentQueue<CapturedCorrelation> Requests { get; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Enqueue(new(request.RequestUri!.AbsolutePath,
            request.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.Single() : null));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"access_token\":\"synthetic-only\",\"token_type\":\"Bearer\",\"expires_in\":900}", Encoding.UTF8, "application/json")
        });
    }
}
