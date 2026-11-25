using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Explicit accountable reason for revoking a permission.</summary>
public sealed record RevokePermissionRequest(string Reason);
