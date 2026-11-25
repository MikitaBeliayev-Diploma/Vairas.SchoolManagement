using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>A branch's school-owned address and metadata.</summary>
public sealed record BranchResponse(Guid BranchId, Guid SchoolId, string Name, string? AddressLine, string? City, string? PostalCode, string? CountryCode, SchoolStatus Status, long Revision);
