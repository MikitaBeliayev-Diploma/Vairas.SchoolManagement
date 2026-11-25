using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolSchedulingService
{
    Task<SchedulingSnapshot> SnapshotAsync(SchedulingSnapshotRequest request,CancellationToken ct);
    Task<SchedulingSnapshot> AccessAsync(Guid school,Guid actor,CancellationToken ct);
    Task<RuleScopeResponse> RuleScopeAsync(RuleScopeRequest request,CancellationToken ct);
    Task<SchoolChangeResponse> ChangeAsync(Guid school,Guid operation,SchoolChangeRequest request,SchoolActor actor,CancellationToken ct);
    Task<SchoolChangeResponse> ResumeAsync(Guid school,Guid operation,CancellationToken ct);
    Task<SchoolChangeResponse> OutcomeAsync(Guid school,Guid operation,SchoolActor actor,CancellationToken ct);
    Task<SchedulingDecision> DecisionAsync(Guid school,Guid operation,CancellationToken ct);
    Task<SchoolChangeResponse> AbortAsync(Guid school,Guid operation,string reason,SchoolActor actor,CancellationToken ct);
}
