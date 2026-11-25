using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class SchoolInvitationDiscoveryTests : BusinessFixture
{
    [Test]
    public async Task Administrator_discovers_invitations_and_continues_without_duplicates()
    {
        for (var i = 0; i < 3; i++)
            await Service.IssueAsync(School, Guid.NewGuid(), new(Guid.NewGuid(), SchoolRole.Teacher, null), Admin, default);
        var first = await Service.SchoolInvitationsAsync(School, 2, null, Admin, default);
        Assert.That(first.Items, Has.Count.EqualTo(2));
        Assert.That(first.NextCursor, Is.Not.Null);
        var second = await Service.SchoolInvitationsAsync(School, 2, first.NextCursor, Admin, default);
        Assert.That(second.Items, Has.Count.EqualTo(1));
        Assert.That(second.NextCursor, Is.Null);
        Assert.That(first.Items.Concat(second.Items).Select(x => x.InvitationId).Distinct().Count(), Is.EqualTo(3));
    }
    [Test]
    public void Foreign_school_and_non_administrator_are_not_visible()
    {
        Business.ThrowsBusinessAsync(() => Service.SchoolInvitationsAsync(Guid.NewGuid(), 20, null, Admin, default));
        Business.ThrowsBusinessAsync(() => Service.SchoolInvitationsAsync(School, 20, null, Actor(Target), default));
        Business.ThrowsBusinessAsync(() => Service.SchoolInvitationsAsync(School, 101, null, Admin, default));
        Business.ThrowsBusinessAsync(() => Service.SchoolInvitationsAsync(School, 20, "invalid", Admin, default));
    }
}
