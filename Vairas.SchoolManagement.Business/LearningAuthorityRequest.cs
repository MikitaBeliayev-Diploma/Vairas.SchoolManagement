namespace Vairas.SchoolManagement.Business;

/// <summary>Trusted Learning workload requests current School authority for a resolved user or integration metadata.</summary>
public sealed record LearningAuthorityRequest(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId = null, Guid? TeacherId = null, Guid? GroupId = null, bool EvidenceRevalidation = false);
