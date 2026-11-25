using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
using Vairas.SchoolManagement.Persistance;

namespace Vairas.SchoolManagement.Tests;

[TestFixture, Category("Unit")]
public sealed class InvitationPaginationTests : BusinessFixture
{
    [Test]
    public async Task Timestamp_ties_across_schools_have_no_missing_or_duplicate_rows()
    {
        var schools = new[] { Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Guid.Parse("00000001-0000-0000-0000-000000000000") };
        var ids = new[] { Guid.Parse("ffffffff-0000-0000-0000-000000000000"), Guid.Parse("80000000-0000-0000-0000-000000000000"), Guid.Parse("7fffffff-0000-0000-0000-000000000000"), Guid.Parse("00000001-ffff-0000-0000-000000000000") };
        var expected = new List<SchoolInvitation>();
        foreach (var sid in schools)
            foreach (var id in ids)
            {
                var invitation = new SchoolInvitation { SchoolId = sid, InvitationId = id, TargetUserId = Target, Role = SchoolRole.Teacher, Status = InvitationStatus.Pending, Revision = 1, CreatedAt = Clock.Now, ExpiresAt = Clock.Now.AddDays(1) };
                expected.Add(invitation);
                Store.Rows.Add(invitation);
                Store.Rows.Add(new SchoolInvitation { SchoolId = sid, InvitationId = Guid.NewGuid(), TargetUserId = Other, CreatedAt = Clock.Now.AddDays(-1), ExpiresAt = Clock.Now.AddDays(1) });
            }
        var seen = new List<(Guid School, Guid Id)>();
        string? cursor = null;
        do
        {
            var page = await Service.MyInvitationsAsync(null, 2, cursor, Actor(Target), default);
            Assert.That(page.Items, Has.Count.EqualTo(2));
            Assert.That(page.Items.All(x => x.TargetUserId == Target), Is.True);
            seen.AddRange(page.Items.Select(x => (x.SchoolId, x.InvitationId)));
            cursor = page.NextCursor;
            Assert.That(seen.Count, Is.LessThanOrEqualTo(expected.Count), "Cursor did not advance.");
        } while (cursor is not null);
        Assert.That(seen, Is.EqualTo(expected.OrderBy(x => x.CreatedAt).ThenBy(x => x.SchoolId).ThenBy(x => x.InvitationId).Select(x => (x.SchoolId, x.InvitationId))));
        Assert.That(Store.InvitationPages.All(x => x.Take == 3 && x.Returned <= 3), Is.True);
    }

    [Test]
    public async Task Deep_cursor_within_one_school_retains_a_bounded_read()
    {
        for (var i = 0; i < 30; i++)
            Store.Rows.Add(new SchoolInvitation { SchoolId = School, InvitationId = Guid.NewGuid(), TargetUserId = Target, Role = SchoolRole.Teacher, Status = InvitationStatus.Pending, Revision = 1, CreatedAt = Clock.Now.AddSeconds(i), ExpiresAt = Clock.Now.AddDays(1) });
        var first = await Service.MyInvitationsAsync(School, 1, null, Actor(Target), default);
        var second = await Service.MyInvitationsAsync(School, 1, first.NextCursor, Actor(Target), default);
        Assert.That(second.Items.Single().CreatedAt, Is.EqualTo(Clock.Now.AddSeconds(1)));
        Assert.That(Store.InvitationPages.Select(x => x.Returned), Is.EqualTo(new[] { 2, 2 }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Provider_query_filters_orders_and_limits_before_materialization(bool cursor)
    {
        using var db = new SchoolDbContext(new DbContextOptionsBuilder<SchoolDbContext>()
            .UseSchool("Host=localhost;Database=query_shape;Username=unused").Options);
        var after = cursor ? new InvitationCursor(Clock.Now, School, Guid.NewGuid()) : null;
        var sql = SchoolStore.OwnInvitationPage(db, School, Target, 3, after).ToQueryString();
        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("WHERE school_id=@").And.Contain("AND target_user_id=@"));
            Assert.That(sql, Does.Contain("ORDER BY created_at,school_id,invitation_id LIMIT @"));
            Assert.That(sql.Contains("AND (created_at,school_id,invitation_id)>"), Is.EqualTo(cursor));
            Assert.That(db.ChangeTracker.Entries(), Is.Empty);
        });
    }
}
