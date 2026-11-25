using Microsoft.EntityFrameworkCore;
using Vairas.SchoolManagement.Persistance;
namespace Vairas.SchoolManagement.Integrations;
public sealed class CoordinationQueue(IDbContextFactory<SchoolDbContext> factory):ICoordinationQueue
{
    public async Task<IReadOnlyList<(Guid School,Guid Operation)>> PendingAsync(CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var rows=await db.Database.SqlQueryRaw<Pending>("SELECT school_id AS \"School\",operation_id AS \"Operation\" FROM public.school_pending_coordination() LIMIT 100").ToArrayAsync(ct);
        return rows.Select(x=>(x.School,x.Operation)).ToArray();
    }
    private sealed record Pending(Guid School,Guid Operation);
}
