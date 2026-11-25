namespace Vairas.SchoolManagement.Domain;

public sealed class TrainingGroup
{
    public Guid GroupId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid? BranchId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string? CategoryCode
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
    public long MembershipRevision
    {
        get; set;
    }
    public Guid LastOperationId
    {
        get; set;
    }
}
