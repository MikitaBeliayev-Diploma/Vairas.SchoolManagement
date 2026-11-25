namespace Vairas.SchoolManagement.Domain;

public sealed class SchoolLearningOutbox
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
}
