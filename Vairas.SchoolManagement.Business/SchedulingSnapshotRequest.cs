using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Authenticated workload asks on behalf of a server-resolved user.</summary>
public sealed record SchedulingSnapshotRequest(SchedulingInput Request,Guid ActorUserId);
