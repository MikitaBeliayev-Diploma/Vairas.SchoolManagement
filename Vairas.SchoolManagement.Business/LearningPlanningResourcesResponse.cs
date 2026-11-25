namespace Vairas.SchoolManagement.Business;

/// <summary>School resources and current enrollment/student pairs; vehicle eligibility remains external.</summary>
public sealed record LearningPlanningResourcesResponse(Guid SchoolId, string Category, Guid BranchId, Guid[] TeacherIds, LearningParticipant[] Participants, DateTimeOffset ValidUntil);
