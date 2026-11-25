using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed record MembershipReviewResponse(Guid ReviewId, Guid SchoolId, string TargetKind, Guid TargetId, string ReasonCode, string SubjectRole,
    Guid SubjectUserId, Guid SourceEventId, DateTimeOffset RaisedAt, DateTimeOffset? ResolvedAt, Guid? ResolvedByUserId, string? ResolutionNote);
