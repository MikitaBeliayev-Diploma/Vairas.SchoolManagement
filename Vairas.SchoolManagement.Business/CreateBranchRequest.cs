using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Create a school branch with optional address.</summary>
public sealed record CreateBranchRequest(string Name, string? AddressLine = null, string? City = null, string? PostalCode = null, string? CountryCode = null);
