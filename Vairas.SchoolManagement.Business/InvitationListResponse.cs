using System.Linq.Expressions;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

/// <summary>Current invitation metadata addressed exclusively to the caller.</summary>
public sealed record InvitationListResponse(IReadOnlyList<InvitationResponse> Items, string? NextCursor, DateTimeOffset ServerTime);
