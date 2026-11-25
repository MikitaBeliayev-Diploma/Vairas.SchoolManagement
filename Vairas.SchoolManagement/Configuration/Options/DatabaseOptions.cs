namespace Vairas.SchoolManagement.Configuration.Options;

/// <summary>Bound from "ConnectionStrings": runtime role plus the separate Learning delivery worker role.</summary>
public sealed class DatabaseOptions
{
    public string SchoolManagement { get; set; } = "";
    public string LearningDelivery { get; set; } = "";
}
