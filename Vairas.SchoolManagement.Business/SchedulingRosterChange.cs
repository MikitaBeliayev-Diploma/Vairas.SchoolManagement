using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Complete future roster under its new immutable revision.</summary>
public sealed record SchedulingRosterChange(Guid LessonId,int ExpectedRevision,long MembershipRevision,SchedulingParticipant[] Participants);
