using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Exclusive ascending keyset boundary, including the cross-school tie breaker.</summary>
public sealed record InvitationCursor(DateTimeOffset Created, Guid School, Guid Id);
