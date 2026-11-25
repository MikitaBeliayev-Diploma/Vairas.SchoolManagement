namespace Vairas.SchoolManagement.Domain;

public sealed class SchoolInvitation
{
    public Guid InvitationId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid TargetUserId
    {
        get; set;
    }
    public SchoolRole Role
    {
        get; set;
    }
    public string? CategoryCode
    {
        get; set;
    }
    public Guid? IssuerMembershipId
    {
        get; set;
    }
    public InvitationStatus Status
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
    public DateTimeOffset ExpiresAt
    {
        get; set;
    }
    public Guid IssuedOperationId
    {
        get; set;
    }
    public Guid? AcceptedOperationId
    {
        get; set;
    }
    public DateTimeOffset? AcceptedAt
    {
        get; set;
    }
    public Guid? ResultMembershipId
    {
        get; set;
    }
    public Guid? ResultEnrollmentId
    {
        get; set;
    }
    public Guid? RevokedOperationId
    {
        get; set;
    }
    public DateTimeOffset? RevokedAt
    {
        get; set;
    }
}
