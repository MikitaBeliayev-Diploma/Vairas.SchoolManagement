using System.Text.Json;
using System.Text.Json.Serialization;
namespace Vairas.SchoolManagement.Business;

/// <summary>Retained canonical roster identity pair.</summary>
public sealed record SchedulingParticipant(Guid EnrollmentId,Guid StudentId);
