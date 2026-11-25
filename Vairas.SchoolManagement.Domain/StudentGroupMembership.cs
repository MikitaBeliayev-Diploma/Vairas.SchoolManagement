namespace Vairas.SchoolManagement.Domain;

public sealed class StudentGroupMembership
{
    public Guid MembershipId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid GroupId
    {
        get; set;
    }
    public DateTimeOffset JoinedAt
    {
        get; set;
    }
    public DateTimeOffset? LeftAt
    {
        get; set;
    }
    public Guid AssociationProgramVersionId
    {
        get; set;
    }
    public DateTimeOffset AssociationVerifiedAt
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
