using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Finite current authorization and exact participant basis.</summary>
public sealed record SchedulingSnapshot(Guid SchoolId,long SchoolRevision,Guid ActingUserId,Guid TeacherId,Guid BranchId,bool Allowed,bool Administrator,bool RuleOwner,bool CanCorrect,bool TeacherAssessor,bool TeacherMember,
    Guid? EnrollmentId,Guid? StudentId,Guid? GroupId,long? MembershipRevision,string Category,SchedulingParticipant[] Participants,DateTimeOffset StartsAt,DateTimeOffset EndsAt,DateTimeOffset ValidUntil);
