using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Configuration;
using Vairas.SchoolManagement.Integrations;
using Vairas.SchoolManagement.Middleware;
using Vairas.SchoolManagement.Persistance;
using Vairas.SchoolManagement.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);
builder.Services.AddSingleton(p => p.GetRequiredService<IConfiguration>().GetSection("Dependencies").Get<DependencyOptions>() ?? new());
builder.Services.AddSingleton(p => p.GetRequiredService<IConfiguration>().GetSection("Access").Get<SchoolAccessOptions>() ?? new());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
    o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    o.JsonSerializerOptions.AllowDuplicateProperties = false;
    o.JsonSerializerOptions.RespectRequiredConstructorParameters = true;
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
{
    var problem = new ProblemDetails { Status = 400, Title = "The request is invalid.", Instance = context.HttpContext.Request.Path };
    problem.Extensions["code"] = "VALIDATION_FAILED";
    problem.Extensions["correlationId"] = CorrelationMiddleware.Id(context.HttpContext);
    return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<ApiDocumentTransformer>());
builder.Services.AddDbContextFactory<SchoolDbContext>((p, o) => SchoolDatabase.Configure(o, p.GetRequiredService<IConfiguration>().GetConnectionString("SchoolManagement")!));
builder.Services.AddScoped<ISchoolStore, SchoolStore>();
builder.Services.AddScoped<ISchoolService, SchoolService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("dependencies", c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton<IWorkloadTokens, WorkloadTokens>();
builder.Services.AddScoped<IDependencyTransport, DependencyTransport>();
builder.Services.AddScoped<IUserActivity, UserActivity>();
builder.Services.AddScoped<ISchoolActorFactory, SchoolActorFactory>();
builder.Services.AddApiSecurity(builder.Configuration);
builder.Services.AddHealthChecks().AddCheck<DatabaseReadiness>("database", tags: ["ready"]);
var telemetry = builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("Vairas.SchoolManagement"))
 .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
 .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) telemetry.UseOtlpExporter();

var app = builder.Build();
StartupValidation.Validate(app.Configuration, app.Environment, app.Services.GetRequiredService<DependencyOptions>(), app.Services.GetRequiredService<SchoolAccessOptions>());
app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
{
    await status.HttpContext.WriteProblemAsync(status.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed.");
});
app.Use(async (http, next) =>
{
    var start = Stopwatch.GetTimestamp();
    await next(http);
    app.Logger.LogInformation("HTTP {Method} {Route} returned {StatusCode} in {ElapsedMs} ms", http.Request.Method,
     (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched", http.Response.StatusCode, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
app.Run();
public partial class Program;
