using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Accepted durable activation; Pending is never represented as an effective membership.</summary>
public sealed record SchoolChangeResponse(Guid OperationId,Guid SchoolId,string State,Guid ResultId,long SchoolRevision);
