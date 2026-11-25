using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Business;

/// <summary>Only a trusted source consumer may request current relationships for source-owned enrollment identifiers.</summary>
public sealed record NotificationRecipientsRequest(Guid SchoolId, Guid[] EnrollmentIds, Guid? TeacherId, string Kind);
