using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Revoke a pending invitation with accountable reason.</summary>
public sealed record RevokeInvitationRequest(long ExpectedRevision, string Reason);
