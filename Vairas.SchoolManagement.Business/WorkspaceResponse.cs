using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Current school membership context for the authenticated user.</summary>
public sealed record WorkspaceResponse(Guid SchoolId, string Name, string[] Roles, string[] Permissions);
