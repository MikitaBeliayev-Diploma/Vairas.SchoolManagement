using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;
public abstract class BusinessFixture
{
    protected FixedClock Clock = null!; protected MemoryStore Store = null!; protected MemoryActivity Activity = null!; protected SchoolAccessOptions Access = null!; protected SchoolService Service = null!;
    protected Guid School, AdminUser, Target, Other, Student, Teacher, Group, Enrollment;
    protected SchoolActor Admin => Actor(AdminUser);
    protected SchoolActor Actor(Guid user) => new(user, Student, Teacher, Guid.NewGuid(), Clock.Now.AddSeconds(30));
    [SetUp]
    public void Setup()
    {
        Clock = new();
        Store = new(Clock);
        Activity = new();
        Access = new();
        Service = new(Store, Activity, Access, Clock);
        School = Guid.NewGuid();
        AdminUser = Guid.NewGuid();
        Target = Guid.NewGuid();
        Other = Guid.NewGuid();
        Student = Guid.NewGuid();
        Teacher = Guid.NewGuid();
        Group = Guid.NewGuid();
        Enrollment = Guid.NewGuid();
        Store.Rows.Add(new School { SchoolId = School, Name = "School", Status = SchoolStatus.Active, Revision = 1, CreatedAt = Clock.Now, UpdatedAt = Clock.Now });
        Member(AdminUser, SchoolRole.Administrator);
    }
    protected SchoolMembership Member(Guid user, SchoolRole role)
    {
        var m = new SchoolMembership { SchoolId = School, MembershipId = Guid.NewGuid(), UserId = user, Role = role, StartedAt = Clock.Now.AddDays(-1), Revision = 1 };
        Store.Rows.Add(m);
        return m;
    }
    protected StudentEnrollment Enroll(Guid? student = null)
    {
        var e = new StudentEnrollment { SchoolId = School, EnrollmentId = Enrollment, StudentId = student ?? Student, CategoryCode = "B", Status = EnrollmentStatus.Active, Revision = 1, StartedAt = Clock.Now.AddDays(-1), StatusChangedAt = Clock.Now.AddDays(-1) };
        Store.Rows.Add(e);
        return e;
    }
    protected TrainingGroup TrainingGroup()
    {
        var g = new TrainingGroup { SchoolId = School, GroupId = Group, Name = "Group", StartedAt = Clock.Now.AddDays(-1), Revision = 1, MembershipRevision = 1 };
        Store.Rows.Add(g);
        Store.Rows.Add(new GroupRosterRevision { SchoolId = School, GroupId = Group, MembershipRevision = 1, RecordedAt = Clock.Now });
        return g;
    }
    protected Task<MutationResponse<InvitationResponse>> Issue(SchoolRole role = SchoolRole.Teacher, Guid? operation = null) => Service.IssueAsync(School, operation ?? Guid.NewGuid(), new(Target, role, role == SchoolRole.Student ? "B" : null), Admin, default);
    protected static BusinessException Error(TestDelegate action, string code)
    {
        var e = Business.ThrowsBusiness(() => action())!;
        Assert.That(e.Code, Is.EqualTo(code));
        return e;
    }
    protected static BusinessException Error(AsyncTestDelegate action, string code)
    {
        var e = Business.ThrowsBusinessAsync(() => action())!;
        Assert.That(e.Code, Is.EqualTo(code));
        return e;
    }
}

