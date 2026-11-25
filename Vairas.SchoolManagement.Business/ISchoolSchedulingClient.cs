using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolSchedulingClient
{
    bool Enabled { get; }
    Task<SchedulingTransitionOutcome> PrepareAsync(SchedulingTransition request,CancellationToken ct);
    Task<SchedulingTransitionOutcome> FinishAsync(Guid school,Guid operation,string decision,CancellationToken ct);
    Task<IReadOnlyList<SchedulingImpact>> ImpactsAsync(Guid school,string kind,Guid target,DateTimeOffset from,CancellationToken ct);
    Task ValidateProgrammeAsync(Guid school,Guid enrollment,Guid group,Guid programme,CancellationToken ct);
}
