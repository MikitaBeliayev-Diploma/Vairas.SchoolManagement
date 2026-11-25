namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class SchoolInboxOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
    public string UserTopic { get; set; } = "vairas.user.deactivated.v1";
    public string Subscription { get; set; } = "school-management";
    public int MaxDeliveryCount { get; set; } = 10;
}
