namespace Vairas.SchoolManagement.Domain;

public sealed class Branch
{
    public Guid BranchId
    {
        get; set;
    }
    public Guid SchoolId
    {
        get; set;
    }
    public string Name { get; set; } = "";
    public string? AddressLine
    {
        get; set;
    }
    public string? City
    {
        get; set;
    }
    public string? PostalCode
    {
        get; set;
    }
    public string? CountryCode
    {
        get; set;
    }
    public SchoolStatus Status
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public Guid LastOperationId
    {
        get; set;
    }
}
