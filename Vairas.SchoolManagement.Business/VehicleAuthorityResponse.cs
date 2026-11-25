using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Current tenant/branch/actor and exact teacher-membership basis; never a booking permit.</summary>
public sealed record VehicleAuthorityResponse(bool Allowed, Guid SchoolId, Guid? BranchId, Guid? ActorUserId, Guid? TeacherId,
    Guid? SchoolMembershipId, Guid? TeacherUserId, long SchoolRevision, long? BranchRevision, long? MembershipRevision, DateTimeOffset ValidUntil);
