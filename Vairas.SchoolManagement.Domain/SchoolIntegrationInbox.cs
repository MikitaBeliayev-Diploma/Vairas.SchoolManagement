namespace Vairas.SchoolManagement.Domain;

/// <summary>Processed external integration event. Global (not school-scoped) so redelivery is idempotent across schools.</summary>
public sealed class SchoolIntegrationInbox
{
    public Guid EventId { get; set; }
    public string Producer { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Outcome { get; set; } = "";
    public int ReviewsRaised { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
