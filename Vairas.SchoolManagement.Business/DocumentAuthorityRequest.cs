using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Trusted document workload requests an exact current human scope.</summary>
public sealed record DocumentAuthorityRequest(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? TeacherId);
