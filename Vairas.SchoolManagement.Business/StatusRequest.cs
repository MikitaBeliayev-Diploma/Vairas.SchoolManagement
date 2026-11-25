using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Eligibility-changing status request, unavailable without coordination.</summary>
public sealed record StatusRequest(long ExpectedRevision, string Status);
