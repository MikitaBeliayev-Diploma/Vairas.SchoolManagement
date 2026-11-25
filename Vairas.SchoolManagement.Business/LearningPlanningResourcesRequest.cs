namespace Vairas.SchoolManagement.Business;

/// <summary>Finite School-owned metadata for Learning's demand builder. It is not a reservation.</summary>
public sealed record LearningPlanningResourcesRequest(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? GroupId, Guid TeacherId);
