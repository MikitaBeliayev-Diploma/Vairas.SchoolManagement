using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Validate that a School rule references an authorized same-school resource.</summary>
public sealed record RuleScopeRequest(Guid SchoolId,Guid ActorUserId,string ScopeKind,Guid? ScopeResourceId);
