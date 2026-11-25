using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class TenantNonDisclosureTests : BusinessFixture
{
    [TestCase(SchoolStatus.Active)]
    [TestCase(SchoolStatus.Inactive)]
    public void Inaccessible_school_has_same_not_found_as_absent_school(SchoolStatus status)
    {
        Store.Rows.OfType<School>().Single().Status = status;
        var foreign = Error(async () => await Service.GetSchoolAsync(School, Actor(Other), default), "NOT_FOUND");
        var absent = Error(async () => await Service.GetSchoolAsync(Guid.NewGuid(), Actor(Other), default), "NOT_FOUND");
        Assert.That(foreign.HttpStatus(), Is.EqualTo(absent.HttpStatus()));
    }

    [Test]
    public void Non_administrator_cannot_infer_inactive_school_from_administrator_operation()
    {
        Member(Target, SchoolRole.Teacher);
        Store.Rows.OfType<School>().Single().Status = SchoolStatus.Inactive;
        Error(async () => await Service.RenameSchoolAsync(School, Guid.NewGuid(), new(1, "Renamed"), Actor(Target), default), "NOT_FOUND");
        Error(async () => await Service.GetSchoolAsync(School, Admin, default), "ACCESS_DENIED");
    }

    [TestCase(SchoolStatus.Active)]
    [TestCase(SchoolStatus.Inactive)]
    public async Task Self_invitation_filter_returns_same_empty_page_for_foreign_and_absent_school(SchoolStatus status)
    {
        await Issue(); // An invitation for a different target must not disclose the school.
        Store.Rows.OfType<School>().Single().Status = status;
        var foreign = await Service.MyInvitationsAsync(School, 10, null, Actor(Other), default);
        var absent = await Service.MyInvitationsAsync(Guid.NewGuid(), 10, null, Actor(Other), default);
        Assert.Multiple(() =>
        {
            Assert.That(foreign.Items, Is.Empty);
            Assert.That(absent.Items, Is.Empty);
            Assert.That(foreign.NextCursor, Is.Null);
            Assert.That(absent.NextCursor, Is.Null);
        });
    }

    [Test]
    public async Task Target_can_list_invitation_without_membership_even_when_school_inactive()
    {
        var issued = await Issue();
        Store.Rows.OfType<School>().Single().Status = SchoolStatus.Inactive;
        var page = await Service.MyInvitationsAsync(School, 10, null, Actor(Target), default);
        Assert.That(page.Items.Select(x => x.InvitationId), Is.EqualTo(new[] { issued.Resource.InvitationId }));
        var missing = Error(async () => await Service.InvitationAsync(Guid.NewGuid(), issued.Resource.InvitationId, Actor(Target), default), "NOT_FOUND");
        Assert.That(missing.HttpStatus(), Is.EqualTo(404));
    }
}
