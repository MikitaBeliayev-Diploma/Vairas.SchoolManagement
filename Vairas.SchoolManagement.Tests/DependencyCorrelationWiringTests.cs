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

[TestFixture, Category("Integration")]
public sealed class DependencyCorrelationWiringTests
{
    [Test]
    public async Task Actual_named_client_propagates_cold_and_refreshed_token_request_ids_without_background_leak()
    {
        var clock = new FixedClock();
        var capture = new CorrelationCapture();
        await using var factory = new CorrelationFactory(clock, capture);
        using var client = factory.CreateClient();

        using var cold = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        cold.Headers.Add("X-Correlation-ID", "cold-token-request");
        using var first = await client.SendAsync(cold);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(capture.Requests.Single(), Is.EqualTo(new CapturedCorrelation("/connect/token", "cold-token-request")));

        using var cached = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        cached.Headers.Add("X-Correlation-ID", "cached-token-request");
        using var second = await client.SendAsync(cached);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(capture.Requests.Count, Is.EqualTo(1), "A still-valid cached token needs no new OAuth call.");

        clock.Now = clock.Now.AddMinutes(20);
        using var refresh = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        refresh.Headers.Add("X-Correlation-ID", "unsafe id!");
        using var third = await client.SendAsync(refresh);
        Assert.That(third.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var normalized = third.Headers.GetValues("X-Correlation-ID").Single();
        Assert.That(normalized, Is.Not.EqualTo("unsafe id!"));
        Assert.That(capture.Requests.Last(), Is.EqualTo(new CapturedCorrelation("/connect/token", normalized)));

        clock.Now = clock.Now.AddMinutes(20);
        await factory.Services.GetRequiredService<IWorkloadTokens>().GetAsync(default);
        Assert.That(capture.Requests.Last(), Is.EqualTo(new CapturedCorrelation("/connect/token", null)));
        Assert.That(capture.Requests.Count, Is.EqualTo(3));
    }

    private static string ApiRoot([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../Vairas.SchoolManagement"));

    private sealed class CorrelationFactory(FixedClock clock, CorrelationCapture capture) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseContentRoot(ApiRoot());
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SchoolManagement"] = "Host=localhost;Database=not_accessed;Username=unused",
                ["Dependencies:AuthBaseUrl"] = "https://authentication.test/",
                ["Dependencies:UserManagementBaseUrl"] = "https://profiles.test/",
                ["Dependencies:ClientSecret"] = "synthetic-correlation-test-only",
                ["Authentication:Authority"] = "https://authentication.test",
                ["Authentication:Issuer"] = "https://authentication.test"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddHttpClient("dependencies").ConfigurePrimaryHttpMessageHandler(() => capture);
                services.AddSingleton<IStartupFilter, TokenRequestProbe>();
            });
        }
    }

    // Trigger token acquisition while the real request middleware's normalized context
    // is active. The probe adds no API endpoint and never queries the database.
    private sealed class TokenRequestProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (http, continuation) =>
            {
                if (http.Request.Path == "/health/live")
                    http.Response.OnStarting(async () =>
                        await http.RequestServices.GetRequiredService<IWorkloadTokens>().GetAsync(http.RequestAborted));
                await continuation();
            });
            next(app);
        };
    }
}
