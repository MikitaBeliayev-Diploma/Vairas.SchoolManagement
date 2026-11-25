using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Stable Scheduling coordination receipt.</summary>
public sealed record SchedulingTransitionOutcome(Guid OperationId,Guid SchoolId,string Status,long Revision);
