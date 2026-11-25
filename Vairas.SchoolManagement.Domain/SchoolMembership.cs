namespace Vairas.SchoolManagement.Domain;

public sealed class SchoolMembership
{
    public Guid MembershipId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid UserId
    {
        get; set;
    }
    public SchoolRole Role
    {
        get; set;
    }
    public DateTimeOffset StartedAt
    {
        get; set;
    }
    public DateTimeOffset? EndedAt
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
