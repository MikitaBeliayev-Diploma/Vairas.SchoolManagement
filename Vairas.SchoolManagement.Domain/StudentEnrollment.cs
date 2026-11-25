namespace Vairas.SchoolManagement.Domain;

public sealed class StudentEnrollment
{
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid StudentId
    {
        get; set;
    }
    public string CategoryCode { get; set; } = "";
    public DateTimeOffset StartedAt
    {
        get; set;
    }
    public DateTimeOffset? EndedAt
    {
        get; set;
    }
    public EnrollmentStatus Status
    {
        get; set;
    }
    public DateTimeOffset StatusChangedAt
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public Guid LastOperationId
    {
        get; set;
    }
}
