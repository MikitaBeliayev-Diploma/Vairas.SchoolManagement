using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed record ProfileBinding(string Kind, Guid ProfileId, Guid UserId, bool Active);
