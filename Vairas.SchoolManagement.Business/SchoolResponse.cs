using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Safe school metadata.</summary>
public sealed record SchoolResponse(Guid SchoolId, string Name, SchoolStatus Status, long Revision);
