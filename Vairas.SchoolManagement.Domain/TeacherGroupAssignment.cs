namespace Vairas.SchoolManagement.Domain;

public sealed class TeacherGroupAssignment
{
    public Guid AssignmentId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid TeacherId
    {
        get; set;
    }
    public Guid GroupId
    {
        get; set;
    }
    public DateTimeOffset AssignedAt
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
