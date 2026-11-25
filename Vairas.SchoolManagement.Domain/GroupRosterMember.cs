namespace Vairas.SchoolManagement.Domain;

public sealed class GroupRosterMember
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
    public Guid MembershipId
    {
        get; set;
    }
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid StudentId
    {
        get; set;
    }
    public long EnrollmentRevision
    {
        get; set;
    }
    public EnrollmentStatus EnrollmentStatus
    {
        get; set;
    }
    public string CategoryCode { get; set; } = "";
    public DateTimeOffset EnrollmentStartedAt
    {
        get; set;
    }
    public DateTimeOffset? EnrollmentEndedAt
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
}
