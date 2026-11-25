using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Canonical enrollment request; new admissions require Scheduling activation.</summary>
public sealed record CreateEnrollmentRequest(Guid StudentId, string LicenseCategory);
