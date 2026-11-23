using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vairas.SchoolManagement.Business;
using Vairas.SchoolManagement.Domain;
namespace Vairas.SchoolManagement.Persistance;

public sealed class SchoolStore(IDbContextFactory<SchoolDbContext> factory) : ISchoolStore
{
    public async Task<ISchoolSession> OpenAsync(Guid schoolId, bool write, CancellationToken ct)
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
        public Task<T?> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => db.Set<T>().SingleOrDefaultAsync(predicate, ct);
        public async Task<IReadOnlyList<T>> ListAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class => await db.Set<T>().Where(predicate).ToArrayAsync(ct);
        public Task<DateTimeOffset> NowAsync(CancellationToken ct) => db.Database.SqlQueryRaw<DateTimeOffset>("""SELECT clock_timestamp() AS "Value" """).SingleAsync(ct);
        public void Add<T>(T entity) where T : class => db.Add(entity);
        public async Task CommitAsync(CancellationToken ct)
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await db.DisposeAsync();
        }
    }
}
