using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>A teacher role identifier authorized for this school's resource selection.</summary>
public sealed record TeacherResourceResponse(Guid TeacherId, Guid UserId);
