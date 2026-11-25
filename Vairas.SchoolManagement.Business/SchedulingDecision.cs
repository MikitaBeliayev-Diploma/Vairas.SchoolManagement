using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Authoritative immutable School decision used by Scheduling reconciliation.</summary>
public sealed record SchedulingDecision(Guid SchoolId,Guid OperationId,long BeforeRevision,long AfterRevision,string PayloadHash,bool LocalApplied,string State);
