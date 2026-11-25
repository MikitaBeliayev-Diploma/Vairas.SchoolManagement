using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Business;

public sealed partial class SchoolService(ISchoolStore store, IUserActivity activity, SchoolAccessOptions access, TimeProvider clock, ISchoolSchedulingService? coordination = null, ISchoolSchedulingClient? schedulingClient = null) : ISchoolService
{
    private bool Operator(Guid user) => access.PlatformOperatorUserIds.Contains(user);
    private async Task<ISchoolSession> Open(Guid school, SchoolActor actor, bool admin, bool write, CancellationToken ct)
    {
        SchoolRules.Id(school);
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        var session = await store.OpenAsync(school, write, ct);
        try
        {
            var now = await session.NowAsync(ct);
            SchoolRules.Fresh(actor, now);
            var memberships = await session.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId, ct);
            if (!Operator(actor.UserId) && !memberships.Any(m => SchoolRules.Effective(m, now) && (!admin || m.Role == SchoolRole.Administrator)))
                throw SchoolErrors.Missing();
            if (write && await HasPendingCoordinationAsync(session, school, ct)) throw SchoolErrors.Dependency();
            var row = await session.FindAsync<School>(x => x.SchoolId == school, ct) ?? throw SchoolErrors.Missing();
            if (row.Status != SchoolStatus.Active)
                throw SchoolErrors.Denied();
            return session;
        }
        catch { await session.DisposeAsync(); throw; }
    }
    private static async Task<bool> HasPendingCoordinationAsync(ISchoolSession session, Guid school, CancellationToken ct) =>
        (await session.ListAsync<SchoolOperation>(x => x.SchoolId == school && x.CoordinationRequired &&
            (x.Status == OperationStatus.Pending || x.Status == OperationStatus.ReconciliationPending), ct)).Count > 0;

    private async Task<SchoolMembership?> Admin(ISchoolSession s, Guid school, Guid user, DateTimeOffset now, CancellationToken ct) =>
     (await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == user && x.Role == SchoolRole.Administrator, ct)).SingleOrDefault(x => SchoolRules.Effective(x, now));
    private async Task<bool> CanGroup(ISchoolSession s, Guid school, Guid group, SchoolActor actor, CancellationToken ct)
    {
        var now = await s.NowAsync(ct);
        if (Operator(actor.UserId) || await Admin(s, school, actor.UserId, now, ct) is not null)
            return true;
        var roles = await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId, ct);
        if (actor.TeacherId is Guid teacher && roles.Any(m => m.Role == SchoolRole.Teacher && SchoolRules.Effective(m, now)) &&
         (await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == school && x.GroupId == group && x.TeacherId == teacher, ct)).Any(x => x.AssignedAt <= now && (x.EndedAt == null || x.EndedAt > now)))
            return true;
        if (actor.StudentId is not Guid student || !roles.Any(m => m.Role == SchoolRole.Student && SchoolRules.Effective(m, now)))
            return false;
        var enrollments = await s.ListAsync<StudentEnrollment>(x => x.SchoolId == school && x.StudentId == student, ct);
        var ids = enrollments.Select(x => x.EnrollmentId).ToArray();
        return (await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == school && x.GroupId == group && ids.Contains(x.EnrollmentId), ct)).Any(x => x.JoinedAt <= now && (x.LeftAt == null || x.LeftAt > now));
    }
    private async Task<SchoolOperation?> Replay(ISchoolSession s, Guid school, Guid operation, string fingerprint, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        var prior = await s.FindAsync<SchoolOperation>(x => x.SchoolId == school && x.OperationId == operation, ct);
        if (prior is not null)
            SchoolRules.Replay(prior, fingerprint, actor.UserId);
        return prior;
    }
    private static SchoolOperation Operation(Guid school, Guid operation, string command, string kind, Guid target, string fingerprint, SchoolActor actor, DateTimeOffset now, long? expected, long result) =>
     new()
     {
         SchoolId = school,
         OperationId = operation,
         CommandName = command,
         TargetKind = kind,
         TargetId = target,
         RequestFingerprint = fingerprint,
         ActorUserId = actor.UserId,
         Channel = OperationChannel.Web,
         Status = OperationStatus.Committed,
         RequestedAt = now,
         ResolvedAt = now,
         ExpectedRevision = expected,
         ResultingRevision = result,
         AuthorityBasis = "current school authority",
         OutcomeCode = "COMMITTED"
     };
    private static async Task Finalize(ISchoolSession s, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Fresh(actor, await s.NowAsync(ct));
        await s.CommitAsync(ct);
    }
    private static SchoolResponse SchoolDto(School s) => new(s.SchoolId, s.Name, s.Status, s.Revision);
    public async Task<SchoolResponse> GetSchoolAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, false, false, ct);
        return SchoolDto((await s.FindAsync<School>(x => x.SchoolId == school, ct))!);
    }
    public async Task<MutationResponse<SchoolResponse>> RenameSchoolAsync(Guid school, Guid operation, RenameRequest request, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        SchoolRules.Revision(request.ExpectedRevision);
        var name = SchoolRules.Text(request.Name, 200);
        var fingerprint = SchoolRules.Fingerprint("RenameSchool", school, new
        {
            request.ExpectedRevision,
            Name = name
        }, actor);
        await using var s = await Open(school, actor, true, true, ct);
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        var row = (await s.FindAsync<School>(x => x.SchoolId == school, ct))!;
        if (prior is not null)
            return new(operation, SchoolDto(row), true);
        SchoolRules.Match(request.ExpectedRevision, row.Revision);
        row.Name = name;
        row.Revision++;
        row.UpdatedAt = await s.NowAsync(ct);
        s.Add(Operation(school, operation, "RenameSchool", "School", school, fingerprint, actor, row.UpdatedAt, request.ExpectedRevision, row.Revision));
        await Finalize(s, actor, ct);
        return new(operation, SchoolDto(row), false);
    }
    public async Task<IReadOnlyList<BranchResponse>> BranchesAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, false, false, ct);
        return (await s.ListAsync<Branch>(x => x.SchoolId == school, ct)).Select(SchoolRules.Branch).OrderBy(x => x.BranchId).ToArray();
    }
    public async Task<BranchResponse> BranchAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        await using var s = await Open(school, actor, false, false, ct);
        return SchoolRules.Branch(await s.FindAsync<Branch>(x => x.SchoolId == school && x.BranchId == id, ct) ?? throw SchoolErrors.Missing());
    }
    public async Task<MutationResponse<BranchResponse>> SaveBranchAsync(Guid school, Guid? id, Guid operation, CreateBranchRequest request, long? expected, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        if (id.HasValue)
        {
            SchoolRules.Id(id.Value);
            SchoolRules.Revision(expected);
        }
        request = new(SchoolRules.Text(request.Name, 200), SchoolRules.Optional(request.AddressLine, 300), SchoolRules.Optional(request.City, 120), SchoolRules.Optional(request.PostalCode, 30), SchoolRules.Optional(request.CountryCode, 2)?.ToUpperInvariant());
        if (request.CountryCode is not null && (request.CountryCode.Length != 2 || request.CountryCode.Any(c => c is < 'A' or > 'Z')))
            throw SchoolErrors.Invalid();
        var command = id.HasValue ? "UpdateBranch" : "CreateBranch";
        var fingerprint = SchoolRules.Fingerprint(command, id, new
        {
            request,
            expected
        }, actor);
        await using var s = await Open(school, actor, true, true, ct);
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        var row = await s.FindAsync<Branch>(x => x.SchoolId == school && x.BranchId == (prior != null ? prior.TargetId : id), ct);
        if (prior is not null)
            return new(operation, SchoolRules.Branch(row ?? throw SchoolErrors.Missing()), true);
        if (id.HasValue)
        {
            if (row is null)
                throw SchoolErrors.Missing();
            SchoolRules.Match(expected!.Value, row.Revision);
            row.Revision++;
        }
        else
        {
            row = new()
            {
                BranchId = Guid.NewGuid(),
                SchoolId = school,
                Status = SchoolStatus.Active,
                Revision = 1
            };
            s.Add(row);
        }
        row.Name = request.Name;
        row.AddressLine = request.AddressLine;
        row.City = request.City;
        row.PostalCode = request.PostalCode;
        row.CountryCode = request.CountryCode;
        row.LastOperationId = operation;
        s.Add(Operation(school, operation, command, "Branch", row.BranchId, fingerprint, actor, await s.NowAsync(ct), expected, row.Revision));
        await Finalize(s, actor, ct);
        return new(operation, SchoolRules.Branch(row), false);
    }
    public async Task<IReadOnlyList<GroupResponse>> GroupsAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, false, false, ct);
        var now = await s.NowAsync(ct);
        var groups = await s.ListAsync<TrainingGroup>(x => x.SchoolId == school, ct);
        if (Operator(actor.UserId) || await Admin(s, school, actor.UserId, now, ct) is not null)
            return groups.Select(SchoolRules.Group).OrderBy(x => x.GroupId).ToArray();
        var roles = await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId, ct);
        var visible = new HashSet<Guid>();
        if (actor.TeacherId is Guid teacher && roles.Any(x => x.Role == SchoolRole.Teacher && SchoolRules.Effective(x, now)))
            visible.UnionWith((await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == school && x.TeacherId == teacher, ct))
             .Where(x => x.AssignedAt <= now && (x.EndedAt == null || x.EndedAt > now)).Select(x => x.GroupId));
        if (actor.StudentId is Guid student && roles.Any(x => x.Role == SchoolRole.Student && SchoolRules.Effective(x, now)))
        {
            var enrollments = (await s.ListAsync<StudentEnrollment>(x => x.SchoolId == school && x.StudentId == student, ct)).Select(x => x.EnrollmentId).ToArray();
            visible.UnionWith((await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == school && enrollments.Contains(x.EnrollmentId), ct))
             .Where(x => x.JoinedAt <= now && (x.LeftAt == null || x.LeftAt > now)).Select(x => x.GroupId));
        }
        return groups.Where(x => visible.Contains(x.GroupId)).Select(SchoolRules.Group).OrderBy(x => x.GroupId).ToArray();
    }
    public async Task<GroupResponse> GroupAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        await using var s = await Open(school, actor, false, false, ct);
        if (!await CanGroup(s, school, id, actor, ct))
            throw SchoolErrors.Missing();
        return SchoolRules.Group(await s.FindAsync<TrainingGroup>(x => x.SchoolId == school && x.GroupId == id, ct) ?? throw SchoolErrors.Missing());
    }
    public async Task<MutationResponse<GroupResponse>> SaveGroupAsync(Guid school, Guid? id, Guid operation, CreateGroupRequest request, long? expected, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        if (id.HasValue)
        {
            SchoolRules.Id(id.Value);
            SchoolRules.Revision(expected);
        }
        if (request.BranchId.HasValue)
            SchoolRules.Id(request.BranchId.Value);
        request = new(SchoolRules.Text(request.Name, 200), request.BranchId, SchoolRules.Optional(request.CategoryCode, 20));
        var command = id.HasValue ? "RenameGroup" : "CreateGroup";
        var fingerprint = SchoolRules.Fingerprint(command, id, new
        {
            request,
            expected
        }, actor);
        await using var s = await Open(school, actor, true, true, ct);
        var prior = await Replay(s, school, operation, fingerprint, actor, ct);
        var row = await s.FindAsync<TrainingGroup>(x => x.SchoolId == school && x.GroupId == (prior != null ? prior.TargetId : id), ct);
        if (prior is not null)
            return new(operation, SchoolRules.Group(row ?? throw SchoolErrors.Missing()), true);
        if (id.HasValue)
        {
            if (row is null)
                throw SchoolErrors.Missing();
            SchoolRules.Match(expected!.Value, row.Revision);
            if (request.BranchId != row.BranchId || request.CategoryCode != row.CategoryCode)
                throw SchoolErrors.Gate();
            row.Revision++;
        }
        else
        {
            if (request.BranchId is Guid branch && await s.FindAsync<Branch>(x => x.SchoolId == school && x.BranchId == branch && x.Status == SchoolStatus.Active, ct) is null)
                throw SchoolErrors.Missing();
            row = new()
            {
                GroupId = Guid.NewGuid(),
                SchoolId = school,
                BranchId = request.BranchId,
                CategoryCode = request.CategoryCode,
                StartedAt = await s.NowAsync(ct),
                Revision = 1,
                MembershipRevision = 1
            };
            s.Add(row);
            s.Add(new GroupRosterRevision { GroupId = row.GroupId, SchoolId = school, MembershipRevision = 1, RecordedAt = row.StartedAt, OperationId = operation });
        }
        row.Name = request.Name;
        row.LastOperationId = operation;
        s.Add(Operation(school, operation, command, "TrainingGroup", row.GroupId, fingerprint, actor, await s.NowAsync(ct), expected, row.Revision));
        await Finalize(s, actor, ct);
        return new(operation, SchoolRules.Group(row), false);
    }
    public async Task<IReadOnlyList<MembershipResponse>> MembershipsAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, true, false, ct);
        return (await s.ListAsync<SchoolMembership>(x => x.SchoolId == school, ct)).Select(x => new MembershipResponse(x.MembershipId, x.UserId, x.Role, x.StartedAt, x.EndedAt, x.Revision)).ToArray();
    }
    public async Task<EnrollmentResponse> EnrollmentAsync(Guid school, Guid id, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(id);
        await using var s = await Open(school, actor, false, false, ct);
        var row = await s.FindAsync<StudentEnrollment>(x => x.SchoolId == school && x.EnrollmentId == id, ct) ?? throw SchoolErrors.Missing();
        if (!Operator(actor.UserId) && actor.StudentId != row.StudentId && await Admin(s, school, actor.UserId, await s.NowAsync(ct), ct) is null)
        {
            if (actor.TeacherId is not Guid teacher)
                throw SchoolErrors.Missing();
            var now = await s.NowAsync(ct);
            var memberships = await s.ListAsync<SchoolMembership>(x => x.SchoolId == school && x.UserId == actor.UserId && x.Role == SchoolRole.Teacher, ct);
            var groups = (await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == school && x.EnrollmentId == id, ct)).Where(x => x.JoinedAt <= now && (x.LeftAt == null || x.LeftAt > now)).Select(x => x.GroupId).ToArray();
            if (!memberships.Any(x => SchoolRules.Effective(x, now)) || !(await s.ListAsync<TeacherGroupAssignment>(x => x.SchoolId == school && x.TeacherId == teacher && groups.Contains(x.GroupId), ct)).Any(x => x.AssignedAt <= now && (x.EndedAt == null || x.EndedAt > now)))
                throw SchoolErrors.Missing();
        }
        return SchoolRules.Enrollment(row);
    }
    public async Task<IReadOnlyList<EnrollmentResponse>> MyEnrollmentsAsync(SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        if (actor.StudentId is not Guid student)
            return [];
        var result = new List<EnrollmentResponse>();
        foreach (var school in await store.DiscoverAsync(actor.UserId, student, ct))
        {
            await using var s = await store.OpenAsync(school, false, ct);
            result.AddRange((await s.ListAsync<StudentEnrollment>(x => x.SchoolId == school && x.StudentId == student, ct)).Select(SchoolRules.Enrollment));
        }
        SchoolRules.Fresh(actor, clock.GetUtcNow());
        return result.OrderBy(x => x.SchoolId).ThenBy(x => x.EnrollmentId).ToArray();
    }
    public async Task<PlanningInputsResponse> PlanningAsync(Guid school, SchoolActor actor, CancellationToken ct)
    {
        await using var s = await Open(school, actor, true, false, ct);
        var now = await s.NowAsync(ct);
        // These owners are evaluated for the selected draft period at submission, not guessed by this preliminary read.
        var missing = new List<string> { "DraftSpecificSchedulingValidation", "LearningRequirementValidation" };
        var groups = await s.ListAsync<TrainingGroup>(x => x.SchoolId == school && x.StartedAt <= now && (x.EndedAt == null || x.EndedAt > now), ct);
        var enrollments = await s.ListAsync<StudentEnrollment>(x => x.SchoolId == school && x.Status == EnrollmentStatus.Active, ct);
        var memberships = await s.ListAsync<StudentGroupMembership>(x => x.SchoolId == school && x.JoinedAt <= now && (x.LeftAt == null || x.LeftAt > now), ct);
        if (groups.Count == 0)
            missing.Add("TrainingGroups");
        if (enrollments.Count == 0)
            missing.Add("ActiveEnrollments");
        var gaps = enrollments.OrderBy(x => x.EnrollmentId).Select(enrollment =>
        {
            var issues = new List<string>();
            var own = memberships.Where(x => x.EnrollmentId == enrollment.EnrollmentId && groups.Any(g => g.GroupId == x.GroupId)).ToArray();
            if (enrollment.StartedAt > now || enrollment.EndedAt <= now) issues.Add("EnrollmentNotEffective");
            if (own.Length == 0) issues.Add("TrainingGroup");
            if (own.Length == 0 || own.Any(x => x.AssociationProgramVersionId == Guid.Empty)) issues.Add("ProgrammeAssociation");
            if (own.Any(x => groups.Any(g => g.GroupId == x.GroupId && g.CategoryCode != null && g.CategoryCode != enrollment.CategoryCode))) issues.Add("GroupCategoryMismatch");
            return new EnrollmentPlanningInputsResponse(enrollment.EnrollmentId, issues);
        }).ToArray();
        return new(school, false, missing) { Enrollments = gaps };
    }
    public async Task RejectActivationAsync(Guid school, string target, Guid id, Guid operation, long? revision, SchoolActor actor, CancellationToken ct)
    {
        SchoolRules.Id(operation);
        SchoolRules.Id(id);
        if (revision.HasValue)
            SchoolRules.Revision(revision);
        await using var s = await Open(school, actor, true, false, ct);
        var exists = target switch
        {
            "Enrollment" => await s.FindAsync<StudentEnrollment>(x => x.SchoolId == school && x.EnrollmentId == id, ct) is not null,
            "Group" => await s.FindAsync<TrainingGroup>(x => x.SchoolId == school && x.GroupId == id, ct) is not null,
            "Membership" => await s.FindAsync<SchoolMembership>(x => x.SchoolId == school && x.MembershipId == id, ct) is not null,
            "Branch" => await s.FindAsync<Branch>(x => x.SchoolId == school && x.BranchId == id, ct) is not null,
            "School" or "NewEnrollment" => true,
            _ => false
        };
        if (!exists)
            throw SchoolErrors.Missing();
        throw SchoolErrors.Gate();
    }
}
