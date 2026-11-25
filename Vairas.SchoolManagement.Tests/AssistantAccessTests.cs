using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class AssistantAccessTests
{
    [Test]
    public async Task Access_requires_actual_active_membership_and_changes_on_end_rejoin_or_permissions()
    {
        var clock = new FixedClock(); var store = new MemoryStore(clock); var school = Guid.NewGuid(); var actor = Guid.NewGuid();
        store.Rows.Add(new School { SchoolId = school, Name = "Synthetic", Status = SchoolStatus.Active });
        var service = new AssistantAccessService(store, clock); Assert.That((await service.ReadAsync(new(school, actor), default)).Allowed, Is.False);
        var member = new SchoolMembership { SchoolId = school, MembershipId = Guid.NewGuid(), UserId = actor, Role = SchoolRole.Administrator, Revision = 1, StartedAt = clock.Now.AddDays(-1) }; store.Rows.Add(member);
        var first = await service.ReadAsync(new(school, actor), default); Assert.That(first.Allowed && first.CanAudit, Is.True);
        store.Rows.Add(new MembershipPermissionGrant { SchoolId = school, MembershipId = member.MembershipId, GrantId = Guid.NewGuid(), Permission = Permission.Assessor, StartedAt = clock.Now });
        var grant = await service.ReadAsync(new(school, actor), default); Assert.That(grant.AccessStamp, Is.Not.EqualTo(first.AccessStamp));
        member.EndedAt = clock.Now; member.Revision++; Assert.That((await service.ReadAsync(new(school, actor), default)).Allowed, Is.False);
        store.Rows.Add(new SchoolMembership { SchoolId = school, MembershipId = Guid.NewGuid(), UserId = actor, Role = SchoolRole.Student, Revision = 1, StartedAt = clock.Now });
        var rejoin = await service.ReadAsync(new(school, actor), default); Assert.That(rejoin.Allowed, Is.True); Assert.That(rejoin.CanAudit, Is.False); Assert.That(rejoin.AccessStamp, Is.Not.EqualTo(first.AccessStamp));
        Assert.That((await service.ReadAsync(new(Guid.NewGuid(), actor), default)).Allowed, Is.False);
    }
}
