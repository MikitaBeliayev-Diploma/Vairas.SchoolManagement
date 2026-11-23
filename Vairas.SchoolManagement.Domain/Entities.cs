namespace Vairas.SchoolManagement.Domain;

public enum SchoolStatus
{
    Active, Inactive
}
public enum SchoolRole
{
    Student, Teacher, Administrator
}
public enum Permission
{
    Assessor, SchoolRuleOwner
}
public enum EnrollmentStatus
{
    Active, Completed, Withdrawn, Transferred, Cancelled
}
public enum OperationStatus
{
    Pending, Committed, Rejected, ReconciliationPending
}
public enum OperationChannel
{
    Web, MCP, Internal
}
public enum InvitationStatus
{
    Pending, Accepted, Revoked
}

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

public sealed class Branch
{
    public Guid BranchId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string? AddressLine
    {
        get; set;
    }
    public string? City
    {
        get; set;
    }
    public string? PostalCode
    {
        get; set;
    }
    public string? CountryCode
    {
        get; set;
    }
    public SchoolStatus Status
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

public sealed class StudentEnrollment
{
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public Guid StudentId
    {
        get; set;
    }
    public string CategoryCode { get; set; } = "";
    public DateTimeOffset StartedAt
    {
        get; set;
    }
    public DateTimeOffset? EndedAt
    {
        get; set;
    }
    public EnrollmentStatus Status
    {
        get; set;
    }
    public DateTimeOffset StatusChangedAt
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

public sealed class EnrollmentStatusHistory
{
    public Guid EnrollmentId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public long EnrollmentRevision
    {
        get; set;
    }
    public EnrollmentStatus? FromStatus
    {
        get; set;
    }
    public EnrollmentStatus ToStatus
    {
        get; set;
    }
    public DateTimeOffset EffectiveAt
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
    public Guid? CompletionEvidenceId
    {
        get; set;
    }
    public long? CompletionDependencyRevision
    {
        get; set;
    }
    public DateTimeOffset? CompletionVerifiedAt
    {
        get; set;
    }
}

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
