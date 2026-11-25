namespace Vairas.SchoolManagement.Business;

/// <summary>One school-owned enrollment/student association.</summary>
public sealed record LearningParticipant(Guid EnrollmentId, Guid StudentId);
