using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Minimal owner projection for document rendering and resource authorization.</summary>
public sealed record DocumentAuthorityResponse(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? StudentId, string SchoolName, string Category,
    string EnrollmentStatus, long SchoolRevision, long EnrollmentRevision, bool Administrator, bool Self, bool AssignedTeacher, DateTimeOffset ValidUntil);
