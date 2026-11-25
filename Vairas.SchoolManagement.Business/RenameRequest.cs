using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Change only a group's display name.</summary>
public sealed record RenameRequest(long ExpectedRevision, string Name);
