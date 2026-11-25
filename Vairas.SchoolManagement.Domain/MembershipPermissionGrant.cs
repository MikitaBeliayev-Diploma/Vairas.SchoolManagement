namespace Vairas.SchoolManagement.Domain;

public sealed class MembershipPermissionGrant
{
    public Guid GrantId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid MembershipId
    {
        get; set;
    }
    public Permission Permission
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
    public Guid GrantedOperationId
    {
        get; set;
    }
    public Guid? EndedOperationId
    {
        get; set;
    }
}
