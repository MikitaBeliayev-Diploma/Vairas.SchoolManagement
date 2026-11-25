using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Durable delta submitted to Scheduling before local activation.</summary>
public sealed record SchedulingTransition(Guid SchoolId,Guid OperationId,Guid ActorUserId,long BeforeRevision,long AfterRevision,string TargetKind,Guid TargetId,DateTimeOffset EffectiveFrom,
    bool ReducesEligibility,string? ResourceKind,Guid? ResourceId,SchedulingRosterChange[] Rosters);
