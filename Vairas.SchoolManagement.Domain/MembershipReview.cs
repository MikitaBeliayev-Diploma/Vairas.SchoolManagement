namespace Vairas.SchoolManagement.Domain;

/// <summary>
/// Administrator work item raised when a member or enrolled student was globally deactivated in User Management.
/// It never ends the membership or enrollment: ending them is an eligibility change that must go through Scheduling
/// coordination, so the administrator decides and then records the resolution once.
/// </summary>
public sealed class MembershipReview
{
    public Guid ReviewId { get; set; }
    public Guid SchoolId { get; set; }
    /// <summary>Membership or Enrollment.</summary>
    public string TargetKind { get; set; } = "";
    public Guid TargetId { get; set; }
    public string ReasonCode { get; set; } = "";
    /// <summary>Student, Teacher or Administrator.</summary>
    public string SubjectRole { get; set; } = "";
    public Guid SubjectUserId { get; set; }
    public Guid SourceEventId { get; set; }
    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}
