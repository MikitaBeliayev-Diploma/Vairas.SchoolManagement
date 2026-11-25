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

[TestFixture, Category("Unit")]
public sealed class DependencyCorrelationTests
{
    [Test]
    public async Task Pooled_handler_uses_each_current_context_and_omits_background_headers()
    {
        var contexts = new HttpContextAccessor();
        var capture = new CorrelationCapture();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        foreach (var id in new[] { "first.request", "second.request" })
        {
            contexts.HttpContext = new DefaultHttpContext();
            contexts.HttpContext.Items["CorrelationId"] = id;
            using var response = await client.GetAsync("https://dependency/" + id);
        }
        contexts.HttpContext = null;
        using var background = new HttpRequestMessage(HttpMethod.Get, "https://dependency/background");
        background.Headers.Add("X-Correlation-ID", "stale-value");
        using var backgroundResponse = await client.SendAsync(background);
        Assert.That(capture.Requests.Select(x => x.Id), Is.EqualTo(new[] { "first.request", "second.request", null }));
        Assert.That(client.DefaultRequestHeaders.Contains("X-Correlation-ID"), Is.False);
    }

    [Test]
    public async Task Concurrent_requests_on_one_handler_keep_their_own_ids()
    {
        var contexts = new HttpContextAccessor();
        var capture = new CorrelationCapture();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Send(string id)
        {
            contexts.HttpContext = new DefaultHttpContext();
            contexts.HttpContext.Items["CorrelationId"] = id;
            await start.Task;
            using var response = await client.GetAsync("https://dependency/" + id);
        }
        var ids = Enumerable.Range(0, 20).Select(x => "concurrent-" + x).ToArray();
        var requests = ids.Select(Send).ToArray();
        start.SetResult();
        await Task.WhenAll(requests);
        Assert.That(capture.Requests.Count, Is.EqualTo(ids.Length));
        foreach (var request in capture.Requests)
            Assert.That(request.Id, Is.EqualTo(request.Path[1..]));
    }

    [TestCase("valid.id_123-456", true)]
    [TestCase("invalid id!", false)]
    [TestCase("", false)]
    public async Task Sends_normalized_middleware_id_instead_of_raw_or_stale_header(string inbound, bool retained)
    {
        var contexts = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        contexts.HttpContext.Request.Headers["X-Correlation-ID"] = inbound;
        var capture = new CorrelationCapture();
        using var client = new HttpClient(new DependencyCorrelationHandler(contexts) { InnerHandler = capture });
        var middleware = new CorrelationMiddleware(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://dependency/normalized");
            request.Headers.Add("X-Correlation-ID", "stale-value");
            using var response = await client.SendAsync(request);
        });
        await middleware.InvokeAsync(contexts.HttpContext, NullLogger<CorrelationMiddleware>.Instance);
        var sent = capture.Requests.Single().Id;
        Assert.That(sent, Is.EqualTo(contexts.HttpContext.Response.Headers["X-Correlation-ID"].ToString()));
        Assert.That(string.IsNullOrWhiteSpace(sent), Is.False);
        Assert.That(sent == inbound, Is.EqualTo(retained));
    }
}
