using Vairas.SchoolManagement.Configuration;
using Vairas.SchoolManagement.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();
builder.Services
    .AddSchoolOptions(builder.Configuration)
    .AddStrictJsonSerialization()
    .AddExceptionHandling()
    .AddOpenApiDocument()
    .AddSchoolDatabase()
    .AddSchoolServices()
    .AddDependencyHttpClients()
    .AddMessageBroker()
    .AddBearerAuthentication()
    .AddEntryAuthorization()
    .AddServiceHealthChecks()
    .AddServiceTelemetry(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(status => status.HttpContext.WriteProblemAsync(status.HttpContext.Response.StatusCode, "HTTP_ERROR", "The request could not be completed."));
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.Use((http, next) => Vairas.Delegation.ReadPreviewDelegation.EnforceAsync(http, next, "school-management"));
app.UseAuthorization();
app.MapControllers();
app.MapServiceHealthChecks();
app.MapOpenApiDocument();
app.Run();

public partial class Program;
