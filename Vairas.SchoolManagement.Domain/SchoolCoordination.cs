namespace Vairas.SchoolManagement.Domain;
public sealed class SchoolCoordination
{
    public Guid OperationId { get; set; }
    public Guid SchoolId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string CommandJson { get; set; } = "";
    public string TransitionJson { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string State { get; set; } = "Pending";
    public long BeforeRevision { get; set; }
    public long AfterRevision { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public Guid ActorUserId { get; set; }
    public Guid ResultId { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
}
