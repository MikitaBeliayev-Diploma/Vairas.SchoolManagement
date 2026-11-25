namespace Vairas.SchoolManagement.Configuration.Options;

public sealed class DependencyOptions
{
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string UserManagementBaseUrl { get; set; } = "https://localhost:7001/";
    public string ClientId { get; set; } = "school-management";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "identity.validate profiles.status.read profiles.binding.read";
}
