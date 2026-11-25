using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Exact read-only Vehicle Management authority query; no membership is created or consumed.</summary>
public sealed record VehicleAuthorityRequest(Guid SchoolId, Guid? BranchId = null, Guid? ActorUserId = null, Guid? TeacherId = null, Guid? SchoolMembershipId = null);
