namespace Vairas.SchoolManagement.Domain;

public sealed class EnrollmentStatusHistory
{
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public long EnrollmentRevision
    {
        get; set;
    }
    public EnrollmentStatus? FromStatus
    {
        get; set;
    }
    public EnrollmentStatus ToStatus
    {
        get; set;
    }
    public DateTimeOffset EffectiveAt
    {
        get; set;
    }
    public DateTimeOffset RecordedAt
    {
        get; set;
    }
    public Guid OperationId
    {
        get; set;
    }
    public Guid? CompletionEvidenceId
    {
        get; set;
    }
    public long? CompletionDependencyRevision
    {
        get; set;
    }
    public DateTimeOffset? CompletionVerifiedAt
    {
        get; set;
    }
}
