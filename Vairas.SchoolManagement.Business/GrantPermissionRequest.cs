using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Grant a special permission to an existing school membership.</summary>
public sealed record GrantPermissionRequest(string Permission);
