namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class SchoolLearningBusOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
    public string TopicName { get; set; } = "vairas.school.events.v1";
}
