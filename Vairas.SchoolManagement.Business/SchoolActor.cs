using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Server-resolved actor; IDs never come from an action body.</summary>
public sealed record SchoolActor(Guid UserId, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId, DateTimeOffset ValidUntil);
