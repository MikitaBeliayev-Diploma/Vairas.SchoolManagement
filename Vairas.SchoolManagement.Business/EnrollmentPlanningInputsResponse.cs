namespace Vairas.SchoolManagement.Business;

/// <summary>School-owned preliminary gaps for one authorized enrollment; no foreign identity or invented legal values.</summary>
public sealed record EnrollmentPlanningInputsResponse(Guid EnrollmentId, IReadOnlyList<string> MissingItems);
