using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace Vairas.SchoolManagement.Configuration;

public sealed class ApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Info.Title = "Vairas School Management API";
        document.Info.Version = "1.0.0";
        document.Info.Description = "School-local organization, target-bound invitations and retained roster evidence. New eligibility activation is unavailable until verified Scheduling coordination exists. No learning hours or credentials are owned here.";
        document.Components ??= new();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["bearerAuth"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" },
            ["workloadBearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "Dedicated internal audience plus required scope and local client allowlist." }
        };
        var blocked = new HashSet<string> { "createEnrollment", "assignEnrollmentGroup", "endEnrollment", "endMembership", "assignTeacher", "changeSchoolStatus", "changeBranchStatus" };
        var creates = new HashSet<string> { "createBranch", "createGroup", "issueInvitation" };
        foreach (var (path, item) in document.Paths)
            foreach (var operation in item.Operations!.Values)
            {
                var internalRoute = path.StartsWith("/internal/", StringComparison.Ordinal);
                operation.Security = [new() { [new OpenApiSecuritySchemeReference(internalRoute ? "workloadBearer" : "bearerAuth", document)] = [] }];
                if (internalRoute)
                    operation.Description = (operation.Description ?? "") + " Required scope: " + (path.Contains("profile-provisioning", StringComparison.Ordinal) ? "school.profile-provisioning" : path.EndsWith("/eligibility", StringComparison.Ordinal) ? "school.eligibility.read" : "school.participants.read") + ". Both signed scope and local workload-client allowlist are mandatory.";
                foreach (var parameter in operation.Parameters ?? [])
                    if (parameter.Name == "Idempotency-Key" && parameter is OpenApiParameter key)
                    {
                        key.Required = true;
                        key.Description = "Nonempty UUID. School-scoped immutable actor/command/payload binding; changed intent conflicts.";
                    }
                operation.Responses ??= new();
                if (operation.OperationId is string creation && creates.Contains(creation))
                    operation.Responses["201"] = new OpenApiResponse
                    {
                        Description = "Created with a Location header. An authorized replay returns 200.",
                        Content = operation.Responses.GetValueOrDefault("200")?.Content,
                        Headers = new Dictionary<string, IOpenApiHeader> { ["Location"] = new OpenApiHeader { Description = "Canonical created resource URL", Schema = new OpenApiSchema { Type = JsonSchemaType.String } } }
                    };
                foreach (var status in new[] { "400", "401", "403", "404", "409", "410", "415", "422", "500", "503" })
                    operation.Responses.TryAdd(status, new OpenApiResponse { Description = "ProblemDetails with stable code and correlationId; foreign existence and secrets are not disclosed.", Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.Object } } } });
                if (operation.OperationId is string id && blocked.Contains(id))
                {
                    operation.Responses.Remove("200");
                    operation.Responses.Remove("204");
                    operation.Description = (operation.Description ?? "") + " This build fails before effects with ACTIVATION_INTEGRATION_NOT_READY; it never reports a fabricated pending operation.";
                }
            }
        return Task.CompletedTask;
    }
}
