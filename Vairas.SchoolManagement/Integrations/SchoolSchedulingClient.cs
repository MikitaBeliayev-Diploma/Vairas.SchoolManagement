using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Vairas.SchoolManagement.Business;
using Microsoft.Extensions.Options;
using Vairas.SchoolManagement.Configuration.Options;
namespace Vairas.SchoolManagement.Integrations;

public sealed class SchoolSchedulingClient(IHttpClientFactory clients,IWorkloadTokens tokens,IOptions<SchedulingDependencyOptions> optionsSettings):ISchoolSchedulingClient
{
    private readonly SchedulingDependencyOptions options = optionsSettings.Value;
    public bool Enabled=>options.Enabled;
    private async Task<T> Send<T>(string origin,string route,object? body,CancellationToken ct)
    {
        if(!Enabled || !Uri.TryCreate(origin,UriKind.Absolute,out var uri) || uri.Scheme!="https")throw SchoolErrors.Gate();
        try
        {
            using var message=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,new Uri(uri,route));message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await tokens.GetAsync(ct));
            if(body is not null)message.Content=JsonContent.Create(body,options:CoordinationWire.Json);
            using var result=await clients.CreateClient("dependencies").SendAsync(message,ct);
            if(result.StatusCode==HttpStatusCode.Conflict)throw SchoolErrors.Conflict("SCHEDULING_REJECTED");
            if(!result.IsSuccessStatusCode)throw SchoolErrors.Dependency();
            return await result.Content.ReadFromJsonAsync<T>(CoordinationWire.Json,ct)??throw SchoolErrors.Dependency();
        }
        catch(Exception e)when(e is HttpRequestException or System.Text.Json.JsonException || e is OperationCanceledException && !ct.IsCancellationRequested){throw SchoolErrors.Dependency();}
    }
    public Task<SchedulingTransitionOutcome> PrepareAsync(SchedulingTransition request,CancellationToken ct)=>Send<SchedulingTransitionOutcome>(options.BaseUrl,"internal/v1/school-eligibility-operations",request,ct);
    public Task<SchedulingTransitionOutcome> FinishAsync(Guid school,Guid operation,string decision,CancellationToken ct)=>Send<SchedulingTransitionOutcome>(options.BaseUrl,$"internal/v1/school-eligibility-operations/{operation:D}/{decision}?schoolId={school:D}",new{},ct);
    public async Task<IReadOnlyList<SchedulingImpact>> ImpactsAsync(Guid school,string kind,Guid target,DateTimeOffset from,CancellationToken ct)=>await Send<SchedulingImpact[]>(options.BaseUrl,$"internal/v1/schools/{school:D}/lesson-impacts?targetKind={Uri.EscapeDataString(kind)}&targetId={target:D}&from={Uri.EscapeDataString(from.ToString("O"))}",null,ct);
    public async Task ValidateProgrammeAsync(Guid school,Guid enrollment,Guid group,Guid programme,CancellationToken ct)
    {
        var result=await Send<ProgrammeDecision>(options.LearningBaseUrl,"internal/v1/scheduling/programme-applicability",new{schoolId=school,enrollmentId=enrollment,groupId=group,programVersionId=programme},ct);
        if(!result.Allowed || result.SchoolId!=school || result.EnrollmentId!=enrollment || result.GroupId!=group || result.ProgramVersionId!=programme || result.ValidUntil<=DateTimeOffset.UtcNow)throw SchoolErrors.Dependency();
    }
    private sealed record ProgrammeDecision(bool Allowed,Guid SchoolId,Guid EnrollmentId,Guid GroupId,Guid ProgramVersionId,DateTimeOffset ValidUntil);
}
