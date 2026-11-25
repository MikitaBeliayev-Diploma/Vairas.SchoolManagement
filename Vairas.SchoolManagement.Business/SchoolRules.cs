using System.Security.Cryptography;
using System.Text.Json;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public static class SchoolRules
{
    public static void Id(Guid id)
    {
        if (id == Guid.Empty)
            throw SchoolErrors.Invalid();
    }
    public static void Revision(long? revision)
    {
        if (revision is null or <= 0)
            throw SchoolErrors.Invalid();
    }
    public static string Text(string? text, int max) => text is null || string.IsNullOrWhiteSpace(text) || text.Trim().Length > max ? throw SchoolErrors.Invalid() : text.Trim();
    public static string? Optional(string? text, int max) => text is null ? null : Text(text, max);
    /// <summary>US-001 / AC-002: reject an enrollment request naming every invalid field; nothing is created.</summary>
    public static void EnrollmentFields(CreateEnrollmentRequest? request)
    {
        var fields = new List<string>();
        if (request is null || request.StudentId == Guid.Empty) fields.Add("studentId");
        if (request is null || string.IsNullOrWhiteSpace(request.LicenseCategory) || request.LicenseCategory.Trim().Length > 20) fields.Add("licenseCategory");
        if (fields.Count > 0) throw new InvalidFieldsException(fields);
    }
    public static void Fresh(SchoolActor actor, DateTimeOffset now)
    {
        Id(actor.UserId);
        if (actor.ValidUntil <= now)
            throw SchoolErrors.Dependency();
    }
    public static bool Covers(DateTimeOffset start, DateTimeOffset? end, DateTimeOffset from, DateTimeOffset to) => start <= from && (end is null || end >= to);
    public static bool Effective(SchoolMembership row, DateTimeOffset now) => row.StartedAt <= now && (row.EndedAt is null || row.EndedAt > now);
    public static void Interval(DateTimeOffset from, DateTimeOffset to)
    {
        if (from >= to || from.Year < 1900 || to.Year > 9998)
            throw SchoolErrors.Invalid();
    }
    public static string Fingerprint<T>(string command, Guid? id, T request, SchoolActor actor) =>
     Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
     {
         version = 1,
         command,
         id,
         request,
         actor.UserId,
         channel = "Web"
     })));
    public static void Replay(SchoolOperation operation, string fingerprint, Guid actor)
    {
        if (operation.ActorUserId != actor || operation.RequestFingerprint != fingerprint)
            throw SchoolErrors.Conflict("OPERATION_KEY_REUSE");
        if (operation.Status == OperationStatus.Rejected)
            throw SchoolErrors.Conflict(operation.OutcomeCode ?? "BUSINESS_RULE_REJECTED");
        if (operation.Status != OperationStatus.Committed)
            throw new ServiceUnavailableException("OUTCOME_UNKNOWN", "The operation has not been finalized.");
    }
    public static void Match(long expected, long actual)
    {
        if (expected != actual)
            throw SchoolErrors.Conflict();
    }
    public static void Issue(IssueInvitationRequest request)
    {
        Id(request.TargetUserId);
        if (!Enum.IsDefined(request.Role))
            throw SchoolErrors.Invalid();
        if (request.Role == SchoolRole.Student)
            Text(request.CategoryCode, 20);
        else if (request.CategoryCode is not null)
            throw SchoolErrors.Invalid();
    }
    public static void Pending(SchoolInvitation invitation, DateTimeOffset now)
    {
        if (invitation.Status != InvitationStatus.Pending)
            throw SchoolErrors.Conflict("INVITATION_TERMINAL");
        if (invitation.ExpiresAt <= now)
            throw new GoneException("INVITATION_EXPIRED", "The invitation has expired.");
    }
    public static InvitationResponse Invitation(SchoolInvitation i, OperationStatus? outcome, DateTimeOffset now) =>
     new(i.InvitationId, i.SchoolId, i.TargetUserId, i.Role, i.CategoryCode, i.Revision, i.CreatedAt, i.ExpiresAt, i.Status,
      i.Status == InvitationStatus.Pending && i.ExpiresAt <= now ? "Expired" : i.Status.ToString(),
      i.Status != InvitationStatus.Accepted ? "NotAccepted" : outcome == OperationStatus.Committed ? "Completed" : "PendingActivation",
      i.AcceptedAt, i.RevokedAt, i.AcceptedOperationId, i.ResultMembershipId, i.ResultEnrollmentId);
    public static BranchResponse Branch(Branch b) => new(b.BranchId, b.SchoolId, b.Name, b.AddressLine, b.City, b.PostalCode, b.CountryCode, b.Status, b.Revision);
    public static GroupResponse Group(TrainingGroup g) => new(g.GroupId, g.SchoolId, g.BranchId, g.Name, g.CategoryCode, g.StartedAt, g.EndedAt, g.Revision, g.MembershipRevision);
    public static EnrollmentResponse Enrollment(StudentEnrollment e) => new(e.EnrollmentId, e.SchoolId, e.StudentId, e.CategoryCode, e.Status, e.Revision, e.StartedAt, e.EndedAt);
}
