using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>One future lesson owned by this school.</summary>
public sealed record SchedulingImpact(Guid LessonId,Guid SchoolId,string Kind,string Status,int Version,DateTimeOffset StartsAt,DateTimeOffset EndsAt,DateTimeOffset? ActualStart,DateTimeOffset? ActualEnd,Guid TeacherId,Guid? EnrollmentId,Guid? StudentId,Guid? VehicleId,Guid? GroupId,long? GroupMembershipRevision);
