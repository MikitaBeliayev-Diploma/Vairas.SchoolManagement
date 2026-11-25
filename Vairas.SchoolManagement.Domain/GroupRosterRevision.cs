namespace Vairas.SchoolManagement.Domain;

public sealed class GroupRosterRevision
{
    public Guid GroupId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public long MembershipRevision
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
}
