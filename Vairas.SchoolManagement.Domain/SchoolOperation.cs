namespace Vairas.SchoolManagement.Domain;

public sealed class SchoolOperation
{
    public Guid OperationId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public string CommandName { get; set; } = "";
    public string TargetKind { get; set; } = "";
    public Guid TargetId
    {
        get; set;
    }
    public long? ExpectedRevision
    {
        get; set;
    }
    public long? ResultingRevision
    {
        get; set;
    }
    public bool CoordinationRequired
    {
        get; set;
    }
    public long? ExpectedSchoolRevision
    {
        get; set;
    }
    public long? ResultingSchoolRevision
    {
        get; set;
    }
    public Guid ActorUserId
    {
        get; set;
    }
    public OperationChannel Channel
    {
        get; set;
    }
    public OperationStatus Status
    {
        get; set;
    }
    public string RequestFingerprint { get; set; } = "";
    public DateTimeOffset RequestedAt
    {
        get; set;
    }
    public DateTimeOffset? ResolvedAt
    {
        get; set;
    }
    public string? Reason
    {
        get; set;
    }
    public string? OutcomeCode
    {
        get; set;
    }
    public string AuthorityBasis { get; set; } = "";
    public Guid? ConfirmationId
    {
        get; set;
    }
    public DateTimeOffset? ConfirmedAt
    {
        get; set;
    }
}
