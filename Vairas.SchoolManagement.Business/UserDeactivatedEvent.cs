using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Validated UserDeactivated envelope; the fingerprint covers the exact delivered body.</summary>
public sealed record UserDeactivatedEvent(Guid EventId, Guid UserId, long UserRevision, DateTimeOffset OccurredAt, string Fingerprint);
