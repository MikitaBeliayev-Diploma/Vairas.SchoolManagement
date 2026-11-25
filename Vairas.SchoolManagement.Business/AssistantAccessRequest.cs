using System.Security.Cryptography;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Minimal tenant access query from the authenticated Chat Backend workload.</summary>
public sealed record AssistantAccessRequest(Guid SchoolId, Guid ActorUserId);
