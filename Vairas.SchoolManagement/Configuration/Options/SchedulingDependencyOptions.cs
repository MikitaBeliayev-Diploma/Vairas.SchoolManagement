namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class SchedulingDependencyOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://localhost:7300/";
    public string LearningBaseUrl { get; set; } = "";
}
