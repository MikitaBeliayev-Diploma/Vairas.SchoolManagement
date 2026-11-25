namespace Vairas.SchoolManagement.Domain;

public sealed class School
{
    public Guid SchoolId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public SchoolStatus Status
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public DateTimeOffset CreatedAt
    {
        get; set;
    }
    public DateTimeOffset UpdatedAt
    {
        get; set;
    }
}
