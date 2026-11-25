using System.Security.Cryptography;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Current access epoch, never an identity or business permission substitute.</summary>
public sealed record AssistantAccessResponse(Guid SchoolId, Guid ActorUserId, bool Allowed, bool CanAudit, string AccessStamp, DateTimeOffset ValidUntil);
