using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Current authorized global UserIds and School display text; not an email authority.</summary>
public sealed record NotificationRecipientsResponse(Guid SchoolId, string SchoolName, long SchoolRevision, Guid[] UserIds, DateTimeOffset ValidUntil);
