using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Optimistic invitation revision for a first acceptance.</summary>
public sealed record AcceptInvitationRequest(long ExpectedRevision);
