namespace Vairas.SchoolManagement.Business;

/// <summary>Exact short-lived authority and prospective/current programme association evidence.</summary>
public sealed record LearningAuthorityResponse(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? StudentId, string Category, string EnrollmentStatus,
    long SchoolRevision, long EnrollmentRevision, bool Allowed, bool Administrator, bool Teacher, bool Assessor, bool Self, bool AssignedTeacher,
    Guid? GroupId, Guid? ProgramVersionId, Guid? BranchId, DateTimeOffset ValidUntil, string AuthorityBasis, DateTimeOffset? RevisionEffectiveAt = null, string? RevisionEffectScope = null);
