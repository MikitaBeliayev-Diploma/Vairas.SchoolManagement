using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public interface ISchoolService
{
    Task<IReadOnlyList<TeacherResourceResponse>> TeachersAsync(Guid school, SchoolActor actor, CancellationToken ct) => throw SchoolErrors.Dependency();
    Task<IReadOnlyList<WorkspaceResponse>> WorkspacesAsync(SchoolActor actor, CancellationToken ct) => throw SchoolErrors.Dependency();
    Task<IReadOnlyList<EnrollmentResponse>> VisibleEnrollmentsAsync(Guid school, SchoolActor actor, CancellationToken ct) => throw SchoolErrors.Dependency();
    Task<VehicleAuthorityResponse> VehicleAuthorityAsync(VehicleAuthorityRequest request, CancellationToken ct);
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
    Task<InvitationListResponse> SchoolInvitationsAsync(Guid school, int limit, string? cursor, SchoolActor actor, CancellationToken ct);
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
