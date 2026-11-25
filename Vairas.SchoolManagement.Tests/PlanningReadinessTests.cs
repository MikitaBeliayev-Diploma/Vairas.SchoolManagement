using NUnit.Framework;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class PlanningReadinessTests : BusinessFixture
{
    [Test]
    public async Task Preliminary_gaps_are_per_enrollment_and_never_include_foreign_or_ended_associations()
    {
        Enroll(); TrainingGroup();
        var foreign = Guid.NewGuid();
        Store.Rows.Add(new StudentEnrollment { SchoolId = Other, EnrollmentId = foreign, Status = EnrollmentStatus.Active });
        Store.Rows.Add(new StudentGroupMembership { SchoolId = School, EnrollmentId = Enrollment, GroupId = Group,
            JoinedAt = Clock.Now.AddDays(-2), LeftAt = Clock.Now.AddDays(-1), AssociationProgramVersionId = Guid.NewGuid() });
        var response = await Service.PlanningAsync(School, Admin, default);
        var gap = response.Enrollments.Single();
        Assert.That(gap.EnrollmentId, Is.EqualTo(Enrollment));
        Assert.That(gap.MissingItems, Is.EquivalentTo(new[] { "TrainingGroup", "ProgrammeAssociation" }));
        Assert.That(response.Complete, Is.False, "School's preliminary read never invents current Scheduling/Learning approval.");
        Store.Rows.Add(new StudentGroupMembership { SchoolId = School, EnrollmentId = Enrollment, GroupId = Group,
            JoinedAt = Clock.Now.AddDays(-1), AssociationProgramVersionId = Guid.NewGuid() });
        Assert.That((await Service.PlanningAsync(School, Admin, default)).Enrollments.Single().MissingItems, Is.Empty);
    }

    [Test]
    public void Unassigned_or_foreign_administrator_cannot_read_readiness_gaps()
    {
        Assert.That(Business.ThrowsBusinessAsync(async () => await Service.PlanningAsync(School, Actor(Other), default))!.HttpStatus(), Is.EqualTo(404));
    }
}
