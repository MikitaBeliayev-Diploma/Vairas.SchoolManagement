using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Persistance;

public sealed class SchoolStore(IDbContextFactory<SchoolDbContext> factory) : ISchoolStore
{
    internal static IQueryable<SchoolInvitation> OwnInvitationPage(SchoolDbContext db, Guid schoolId, Guid targetUserId, int take, InvitationCursor? after)
    {
        if (take is < 1 or > 101) throw new ArgumentOutOfRangeException(nameof(take));
        // PostgreSQL uuid ordering matches Guid.CompareTo (unsigned RFC fields), so
        // the database keyset and the service's cross-school merge use the same order.
        return after is null
            ? db.Set<SchoolInvitation>().FromSql($"""
                SELECT * FROM school_invitation
                WHERE school_id={schoolId} AND target_user_id={targetUserId}
                ORDER BY created_at,school_id,invitation_id LIMIT {take}
                """).AsNoTracking()
            : db.Set<SchoolInvitation>().FromSql($"""
                SELECT * FROM school_invitation
                WHERE school_id={schoolId} AND target_user_id={targetUserId}
                  AND (created_at,school_id,invitation_id)>({after.Created},{after.School},{after.Id})
                ORDER BY created_at,school_id,invitation_id LIMIT {take}
                """).AsNoTracking();
    }
    public Task<ISchoolSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(() => OpenCoreAsync(schoolId, write, ct));
    private async Task<ISchoolSession> OpenCoreAsync(Guid schoolId, bool write, CancellationToken ct)
    {
        var db = await factory.CreateDbContextAsync(ct);
        if (!write)
            db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        try
        {
            var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('vairas.school_id',{schoolId.ToString("D")},true)", ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
            if (write)
                await db.Set<School>().FromSqlInterpolated($"SELECT * FROM school WHERE school_id={schoolId} FOR UPDATE").LoadAsync(ct);
            return new Session(db, transaction);
        }
        catch { await db.DisposeAsync(); throw; }
    }
    public async Task<IReadOnlyList<Guid>> DiscoverAsync(Guid userId, Guid? studentId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Database.SqlQuery<Guid>($"""SELECT school_id AS "Value" FROM public.school_self_discovery({userId},{studentId})""").ToArrayAsync(ct);
    }
    private sealed class Session(SchoolDbContext db, IDbContextTransaction transaction) : ISchoolSession
    {
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => DatabaseExceptionTranslator.GuardAsync(() => db.Set<T>().SingleOrDefaultAsync(predicate, ct));
        public async Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => await DatabaseExceptionTranslator.GuardAsync<IReadOnlyList<T>>(async () => await db.Set<T>().Where(predicate).ToArrayAsync(ct));
        public async Task<IReadOnlyList<SchoolInvitation>> ListOwnInvitationsAsync(Guid schoolId, Guid targetUserId, int take, InvitationCursor? after, CancellationToken ct) =>
            await OwnInvitationPage(db, schoolId, targetUserId, take, after).ToArrayAsync(ct);
        public async Task<IReadOnlyList<SchoolInvitation>> ListSchoolInvitationsAsync(Guid schoolId, int take, InvitationCursor? after, CancellationToken ct)
        {
            var query = db.Set<SchoolInvitation>().Where(x => x.SchoolId == schoolId);
            if (after is not null)
                query = query.Where(x => x.CreatedAt > after.Created || x.CreatedAt == after.Created && x.InvitationId.CompareTo(after.Id) > 0);
            return await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.InvitationId).Take(take).ToArrayAsync(ct);
        }
        public Task<DateTimeOffset> NowAsync(CancellationToken ct) => db.Database.SqlQueryRaw<DateTimeOffset>("""SELECT clock_timestamp() AS "Value" """).SingleAsync(ct);
        public void Add<T>(T entity) where T : class => db.Add(entity);
        public Task CommitAsync(CancellationToken ct) => DatabaseExceptionTranslator.GuardAsync(async () =>
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await db.DisposeAsync();
        }
    }
}

