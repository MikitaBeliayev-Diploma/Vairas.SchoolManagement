using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Exact owner/resource echo; no admission is inferred from existence alone.</summary>
public sealed record RuleScopeResponse(bool Allowed,Guid SchoolId,Guid ActorUserId,string ScopeKind,Guid? ScopeResourceId);
