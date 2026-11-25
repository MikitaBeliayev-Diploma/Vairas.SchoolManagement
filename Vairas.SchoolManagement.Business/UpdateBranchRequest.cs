using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Change branch metadata without changing operational eligibility.</summary>
public sealed record UpdateBranchRequest(long ExpectedRevision, string Name, string? AddressLine = null, string? City = null, string? PostalCode = null, string? CountryCode = null);
