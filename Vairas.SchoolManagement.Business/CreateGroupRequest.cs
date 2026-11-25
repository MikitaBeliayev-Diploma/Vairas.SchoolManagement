using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Create an empty group. Category remains an externally owned code.</summary>
public sealed record CreateGroupRequest(string Name, Guid? BranchId = null, string? CategoryCode = null);
