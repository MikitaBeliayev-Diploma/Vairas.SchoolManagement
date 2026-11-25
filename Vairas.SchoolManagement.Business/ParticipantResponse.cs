using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>A retained enrollment/student pair for a complete requested interval.</summary>
public sealed record ParticipantResponse(Guid EnrollmentId, Guid StudentId);
