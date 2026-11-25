using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Teacher assignment is distinct from lesson assignment and Assessor permission.</summary>
public sealed record TeacherAssignmentRequest(Guid TeacherId, long ExpectedRevision);
