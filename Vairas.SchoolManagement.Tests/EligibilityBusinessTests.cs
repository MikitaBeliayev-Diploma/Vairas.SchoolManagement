using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class EligibilityBusinessTests : BusinessFixture
{
    [Test]
    public async Task Roster_requires_exact_retained_revision_and_full_interval()
    {
        TrainingGroup();
        var from = Clock.Now;
        var to = from.AddHours(1);
        Store.Rows.Add(new GroupRosterMember
        {
            SchoolId = School,
            GroupId = Group,
            MembershipRevision = 1,
            MembershipId = Guid.NewGuid(),
            EnrollmentId = Enrollment,
            StudentId = Student,
            EnrollmentRevision = 1,
            CategoryCode = "B",
            EnrollmentStatus = EnrollmentStatus.Active,
            EnrollmentStartedAt = from.AddDays(-1),
            JoinedAt = from.AddDays(-1)
        });
        Store.Rows.Add(new GroupRosterMember
        {
            SchoolId = School,
            GroupId = Group,
            MembershipRevision = 1,
            MembershipId = Guid.NewGuid(),
            EnrollmentId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            EnrollmentRevision = 1,
            CategoryCode = "B",
            EnrollmentStatus = EnrollmentStatus.Active,
            EnrollmentStartedAt = from.AddDays(-1),
            JoinedAt = from.AddMinutes(1)
        });
        Assert.That((await Service.ParticipantsAsync(School, Group, null, from, to, default)).Participants.Count, Is.EqualTo(1));
        Error(async () => await Service.ParticipantsAsync(School, Group, 99, from, to, default), "NOT_FOUND");
        Error(async () => await Service.ParticipantsAsync(School, Group, null, to, from, default), "VALIDATION_FAILED");
        Error(async () => await Service.ParticipantsAsync(School, Group, null, from.AddDays(-2), to, default), "GROUP_INTERVAL_INVALID");
        Store.Rows.Add(new SchoolOperation { SchoolId = School, CoordinationRequired = true, Status = OperationStatus.Pending });
        Error(async () => await Service.ParticipantsAsync(School, Group, null, from, to, default), "DEPENDENCY_UNAVAILABLE");
        Assert.That((await Service.ParticipantsAsync(School, Group, 1, from, to, default)).Participants.Count, Is.EqualTo(1));
    }
    [Test]
    public void Same_student_two_enrollments_in_one_roster_is_ambiguous()
    {
        TrainingGroup();
        for (var n = 0; n < 2; n++)
            Store.Rows.Add(new GroupRosterMember { SchoolId = School, GroupId = Group, MembershipRevision = 1, MembershipId = Guid.NewGuid(), EnrollmentId = Guid.NewGuid(), StudentId = Student, EnrollmentStartedAt = Clock.Now.AddDays(-1), JoinedAt = Clock.Now.AddDays(-1) });
        Error(async () => await Service.ParticipantsAsync(School, Group, 1, Clock.Now, Clock.Now.AddHours(1), default), "AMBIGUOUS_PARTICIPANT");
    }
    [Test]
    public async Task Eligibility_checks_current_bindings_activity_and_school_roles()
    {
        Enroll();
        Member(Target, SchoolRole.Student);
        Member(Other, SchoolRole.Teacher);
        Activity.Bindings[Student] = new("Student", Student, Target, true);
        Activity.Bindings[Teacher] = new("Teacher", Teacher, Other, true);
        var result = await Service.EligibilityAsync(School, Enrollment, Target, Teacher, default);
        Assert.That(result.Active, Is.True);
        Assert.That(result.TeacherAllowed, Is.True);
        Assert.That((await Service.EligibilityAsync(School, Enrollment, Other, Teacher, default)).Active, Is.True);
        Assert.That((await Service.EligibilityAsync(School, Enrollment, AdminUser, Teacher, default)).Active, Is.True);
        Activity.Inactive.Add(Target);
        Error(async () => await Service.EligibilityAsync(School, Enrollment, AdminUser, Teacher, default), "ACCESS_DENIED");
        Activity.Inactive.Clear();
        Activity.Bindings[Student] = new("Student", Student, Target, false);
        Error(async () => await Service.EligibilityAsync(School, Enrollment, AdminUser, Teacher, default), "ACCESS_DENIED");
    }
    [Test]
    public async Task Inactive_enrollment_is_not_eligible_and_unknown_actor_is_hidden()
    {
        var e = Enroll();
        e.Status = EnrollmentStatus.Withdrawn;
        e.EndedAt = Clock.Now;
        Member(Target, SchoolRole.Student);
        Activity.Bindings[Student] = new("Student", Student, Target, true);
        Activity.Bindings[Teacher] = new("Teacher", Teacher, Other, true);
        Assert.That((await Service.EligibilityAsync(School, Enrollment, Target, Teacher, default)).Active, Is.False);
        Error(async () => await Service.EligibilityAsync(School, Enrollment, Guid.NewGuid(), Teacher, default), "NOT_FOUND");
        Store.Rows.Add(new SchoolOperation { SchoolId = School, CoordinationRequired = true, Status = OperationStatus.Pending });
        Error(async () => await Service.EligibilityAsync(School, Enrollment, AdminUser, Teacher, default), "NOT_FOUND");
    }
    [Test]
    public void Rules_reject_empty_ids_bad_revision_and_invalid_invitation_shape()
    {
        Error(() => SchoolRules.Id(Guid.Empty), "VALIDATION_FAILED");
        Error(() => SchoolRules.Revision(null), "VALIDATION_FAILED");
        Error(() => SchoolRules.Revision(0), "VALIDATION_FAILED");
        Error(() => SchoolRules.Text(" ", 20), "VALIDATION_FAILED");
        Error(() => SchoolRules.Text("too long", 2), "VALIDATION_FAILED");
        Error(() => SchoolRules.Issue(new(Target, (SchoolRole)99, null)), "VALIDATION_FAILED");
        Error(() => SchoolRules.Issue(new(Target, SchoolRole.Teacher, "B")), "VALIDATION_FAILED");
        Error(() => SchoolRules.Issue(new(Target, SchoolRole.Student, null)), "VALIDATION_FAILED");
        Error(() => SchoolRules.Replay(new() { ActorUserId = Target, RequestFingerprint = "a", Status = OperationStatus.Rejected }, "a", Target), "BUSINESS_RULE_REJECTED");
        Error(() => SchoolRules.Replay(new() { ActorUserId = Target, RequestFingerprint = "a", Status = OperationStatus.Pending }, "a", Target), "OUTCOME_UNKNOWN");
        Assert.That(SchoolRules.Covers(Clock.Now, null, Clock.Now, Clock.Now.AddDays(1)), Is.True);
    }
}
