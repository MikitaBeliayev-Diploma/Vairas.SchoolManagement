using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed class SchoolException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static SchoolException Missing() => new(404, "NOT_FOUND", "Resource not found or not visible.");
    public static SchoolException Denied() => new(403, "ACCESS_DENIED", "Access denied.");
    public static SchoolException Dependency() => new(503, "DEPENDENCY_UNAVAILABLE", "A required dependency is unavailable.");
    public static SchoolException Gate() => new(503, "ACTIVATION_INTEGRATION_NOT_READY", "Scheduling eligibility coordination is not available.");
    public static SchoolException Conflict(string code = "REVISION_CONFLICT") => new(409, code, "The command conflicts with current state.");
    public static SchoolException Invalid() => new(400, "VALIDATION_FAILED", "The request is invalid.");
}

/// <summary>Server-resolved actor; IDs never come from an action body.</summary>
public sealed record SchoolActor(Guid UserId, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId, DateTimeOffset ValidUntil);
public sealed class SchoolAccessOptions
{
    public Guid[] PlatformOperatorUserIds { get; set; } = [];
}
public interface IUserActivity
{
    Task RequireActiveAsync(Guid userId, CancellationToken ct); Task<ProfileBinding> BindingAsync(string kind, Guid profileId, CancellationToken ct);
}
public sealed record ProfileBinding(string Kind, Guid ProfileId, Guid UserId, bool Active);
public interface ISchoolStore
{
    Task<ISchoolSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct);
    Task<IReadOnlyList<Guid>> DiscoverAsync(Guid userId, Guid? studentId, CancellationToken ct);
}
public interface ISchoolSession : IAsyncDisposable
{
    Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class;
    Task<DateTimeOffset> NowAsync(CancellationToken ct);
    void Add<T>(T entity) where T : class;
    Task CommitAsync(CancellationToken ct);
}

/// <summary>Safe school metadata.</summary>
public sealed record SchoolResponse(Guid SchoolId, string Name, SchoolStatus Status, long Revision);
/// <summary>A branch's school-owned address and metadata.</summary>
public sealed record BranchResponse(Guid BranchId, Guid SchoolId, string Name, string? AddressLine, string? City, string? PostalCode, string? CountryCode, SchoolStatus Status, long Revision);
/// <summary>Group metadata. MembershipVersion selects a retained roster basis.</summary>
public sealed record GroupResponse(Guid GroupId, Guid SchoolId, Guid? BranchId, string Name, string? CategoryCode, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long Revision, long MembershipVersion);
/// <summary>School-local role; a global profile alone grants no authority.</summary>
public sealed record MembershipResponse(Guid MembershipId, Guid UserId, SchoolRole Role, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, long Revision);
/// <summary>One school-specific enrollment; no learning hours or programme ownership.</summary>
public sealed record EnrollmentResponse(Guid EnrollmentId, Guid SchoolId, Guid StudentId, string LicenseCategory, EnrollmentStatus Status, long Version, DateTimeOffset StartedAt, DateTimeOffset? EndedAt);
/// <summary>Exact target-bound invitation with derived expiration and activation state.</summary>
public sealed record InvitationResponse(Guid InvitationId, Guid SchoolId, Guid TargetUserId, SchoolRole Role, string? CategoryCode, long Revision,
 DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, InvitationStatus Status, string EffectiveStatus, string AcceptanceState,
 DateTimeOffset? AcceptedAt, DateTimeOffset? RevokedAt, Guid? AcceptedOperationId, Guid? ResultMembershipId, Guid? ResultEnrollmentId);
/// <summary>Committed local mutation and its current authorized representation.</summary>
public sealed record MutationResponse<T>(Guid OperationId, T Resource, bool Replayed);
/// <summary>Canonical result of accepting one invitation.</summary>
public sealed record AcceptanceResponse(Guid OperationId, Guid SchoolId, Guid InvitationId, long InvitationRevision, OperationStatus OperationStatus, string Outcome, bool LocalApplied, Guid? ResultMembershipId, Guid? ResultEnrollmentId);
/// <summary>A retained enrollment/student pair for a complete requested interval.</summary>
public sealed record ParticipantResponse(Guid EnrollmentId, Guid StudentId);
/// <summary>Immutable roster revision, filtered for the entire interval.</summary>
public sealed record ParticipantsResponse(Guid GroupId, Guid SchoolId, long MembershipVersion, IReadOnlyList<ParticipantResponse> Participants);
/// <summary>Local eligibility, not a reservation or a reusable authorization permit.</summary>
public sealed record EligibilityResponse(Guid EnrollmentId, Guid SchoolId, Guid StudentId, bool Active, string Category, long Version, bool TeacherAllowed);
/// <summary>Missing planning dependencies; absent providers never imply completeness.</summary>
public sealed record PlanningInputsResponse(Guid SchoolId, bool Complete, IReadOnlyList<string> MissingItems);
/// <summary>Current invitation metadata addressed exclusively to the caller.</summary>
public sealed record InvitationListResponse(IReadOnlyList<InvitationResponse> Items, string? NextCursor, DateTimeOffset ServerTime);
/// <summary>Read-only staff provisioning request compatible with existing User Management.</summary>
public sealed record SchoolAuthorityRequest(Guid ActorUserId, Guid TargetUserId, Guid SchoolId, Guid? InvitationId, SchoolRole Role, bool Bootstrap);
/// <summary>Exact binding echo; validation grants no membership and consumes no invitation.</summary>
public sealed record SchoolAuthorityResponse(bool Allowed, Guid ActorUserId, Guid TargetUserId, Guid SchoolId, Guid? InvitationId, SchoolRole Role, bool Bootstrap, string AuthorityBasis, DateTimeOffset ValidUntil);
/// <summary>Issue a target-bound invitation; server controls expiry and issuer.</summary>
public sealed record IssueInvitationRequest(Guid TargetUserId, SchoolRole Role, string? CategoryCode = null);
/// <summary>Optimistic invitation revision for a first acceptance.</summary>
public sealed record AcceptInvitationRequest(long ExpectedRevision);
/// <summary>Revoke a pending invitation with accountable reason.</summary>
public sealed record RevokeInvitationRequest(long ExpectedRevision, string Reason);
/// <summary>Create a school branch with optional address.</summary>
public sealed record CreateBranchRequest(string Name, string? AddressLine = null, string? City = null, string? PostalCode = null, string? CountryCode = null);
/// <summary>Change branch metadata without changing operational eligibility.</summary>
public sealed record UpdateBranchRequest(long ExpectedRevision, string Name, string? AddressLine = null, string? City = null, string? PostalCode = null, string? CountryCode = null);
/// <summary>Create an empty group. Category remains an externally owned code.</summary>
public sealed record CreateGroupRequest(string Name, Guid? BranchId = null, string? CategoryCode = null);
/// <summary>Change only a group's display name.</summary>
public sealed record RenameRequest(long ExpectedRevision, string Name);
/// <summary>Canonical enrollment request; new admissions require Scheduling activation.</summary>
public sealed record CreateEnrollmentRequest(Guid StudentId, string LicenseCategory);
/// <summary>Group/programme association requires Learning and Scheduling validation.</summary>
public sealed record GroupAssignmentRequest(Guid GroupId, Guid ProgramVersionId, long ExpectedRevision);
/// <summary>Eligibility-changing status request, unavailable without coordination.</summary>
public sealed record StatusRequest(long ExpectedRevision, string Status);
/// <summary>Teacher assignment is distinct from lesson assignment and Assessor permission.</summary>
public sealed record TeacherAssignmentRequest(Guid TeacherId, long ExpectedRevision);

public interface ISchoolService
{
    Task<SchoolResponse> GetSchoolAsync(Guid school, SchoolActor actor, CancellationToken ct);
    Task<IReadOnlyList<BranchResponse>> BranchesAsync(Guid school, SchoolActor actor, CancellationToken ct);
    Task<BranchResponse> BranchAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct);
    Task<MutationResponse<BranchResponse>> SaveBranchAsync(Guid school, Guid? id, Guid operation, CreateBranchRequest request, long? expected, SchoolActor actor, CancellationToken ct);
    Task<MutationResponse<SchoolResponse>> RenameSchoolAsync(Guid school, Guid operation, RenameRequest request, SchoolActor actor, CancellationToken ct);
    Task<IReadOnlyList<GroupResponse>> GroupsAsync(Guid school, SchoolActor actor, CancellationToken ct);
    Task<GroupResponse> GroupAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct);
    Task<MutationResponse<GroupResponse>> SaveGroupAsync(Guid school, Guid? id, Guid operation, CreateGroupRequest request, long? expected, SchoolActor actor, CancellationToken ct);
    Task<IReadOnlyList<MembershipResponse>> MembershipsAsync(Guid school, SchoolActor actor, CancellationToken ct);
    Task<EnrollmentResponse> EnrollmentAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponse>> MyEnrollmentsAsync(SchoolActor actor, CancellationToken ct);
    Task<MutationResponse<InvitationResponse>> IssueAsync(Guid school, Guid operation, IssueInvitationRequest request, SchoolActor actor, CancellationToken ct);
    Task<InvitationResponse> InvitationAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct);
    Task<InvitationListResponse> MyInvitationsAsync(Guid? school, int limit, string? cursor, SchoolActor actor, CancellationToken ct);
    Task<MutationResponse<InvitationResponse>> RevokeAsync(Guid school, Guid id, Guid operation, RevokeInvitationRequest request, SchoolActor actor, CancellationToken ct);
    Task<AcceptanceResponse> AcceptAsync(Guid school, Guid id, Guid operation, AcceptInvitationRequest request, SchoolActor actor, CancellationToken ct);
    Task<SchoolAuthorityResponse> ProvisioningAsync(SchoolAuthorityRequest request, CancellationToken ct);
    Task<ParticipantsResponse> ParticipantsAsync(Guid school, Guid group, long? revision, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<EligibilityResponse> EligibilityAsync(Guid school, Guid enrollment, Guid actor, Guid teacher, CancellationToken ct);
    Task<PlanningInputsResponse> PlanningAsync(Guid school, SchoolActor actor, CancellationToken ct);
    Task RejectActivationAsync(Guid school, string target, Guid id, Guid operation, long? revision, SchoolActor actor, CancellationToken ct);
}
