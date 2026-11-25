using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Immutable roster revision, filtered for the entire interval.</summary>
public sealed record ParticipantsResponse(Guid GroupId, Guid SchoolId, long MembershipVersion, IReadOnlyList<ParticipantResponse> Participants);
