using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Exact owner-scoped allocation for a finite School authorization decision.</summary>
public sealed record SchedulingInput(Guid SchoolId,Guid BranchId,Guid TeacherId,string Category,DateTimeOffset StartsAt,DateTimeOffset EndsAt,Guid? EnrollmentId=null,Guid? StudentId=null,Guid? VehicleId=null,Guid? GroupId=null);
