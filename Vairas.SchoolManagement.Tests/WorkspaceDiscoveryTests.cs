using NUnit.Framework;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class WorkspaceDiscoveryTests : BusinessFixture
{
    [Test]
    public async Task Discovery_requires_effective_membership_and_returns_school_local_roles()
    {
        var own = await Service.WorkspacesAsync(Admin, default);
        Assert.That(own.Single().SchoolId, Is.EqualTo(School));
        Assert.That(own.Single().Roles, Contains.Item("Administrator"));
        Assert.That(await Service.WorkspacesAsync(Actor(Other), default), Is.Empty);
        Store.Rows.OfType<School>().Single().Status = SchoolStatus.Inactive;
        Assert.That(await Service.WorkspacesAsync(Admin, default), Is.Empty);
    }
    [Test]
    public async Task Ended_membership_does_not_remain_a_workspace()
    {
        foreach (var row in Store.Rows.OfType<SchoolMembership>()) row.EndedAt = Clock.Now;
        Assert.That(await Service.WorkspacesAsync(Admin, default), Is.Empty);
    }
}
