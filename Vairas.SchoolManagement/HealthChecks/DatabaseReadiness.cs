using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vairas.SchoolManagement.Persistance;
namespace Vairas.SchoolManagement.HealthChecks;

public sealed class DatabaseReadiness(IDbContextFactory<SchoolDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var valid = await db.Database.SqlQueryRaw<bool>("""
    SELECT (NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb AND NOT r.rolcreaterole
      AND NOT has_schema_privilege('public','CREATE')
      AND NOT has_database_privilege(current_database(),'TEMP')
      AND (SELECT count(*) FROM pg_tables WHERE schemaname='public' AND rowsecurity AND tablename IN
      ('school','school_operation','branch','school_membership','membership_permission_grant','student_enrollment','enrollment_status_history',
       'training_group','student_group_membership','teacher_group_assignment','group_roster_revision','group_roster_member','school_invitation'))=13
      AND NOT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public' AND tableowner=current_user)
      AND to_regprocedure('public.school_self_discovery(uuid,uuid)') IS NOT NULL AND to_regclass('public.membership_review') IS NOT NULL) AS "Value"
    FROM pg_roles r WHERE r.rolname=current_user
    """).SingleAsync(ct);
            return valid ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database schema, isolation or runtime role is not ready.");
        }
        catch { return HealthCheckResult.Unhealthy("Database is unavailable."); }
    }
}
