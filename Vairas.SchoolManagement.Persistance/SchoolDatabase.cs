using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Vairas.SchoolManagement.Domain;

namespace Vairas.SchoolManagement.Persistance;

public static class SchoolDatabase
{
    // A shared translator keeps Npgsql's EF service-provider cache stable across contexts.
    private static readonly NpgsqlNullNameTranslator Names = new();

    public static DbContextOptionsBuilder<SchoolDbContext> UseSchool(
        this DbContextOptionsBuilder<SchoolDbContext> builder, string connection)
    {
        Configure(builder, connection);
        return builder;
    }

    public static void Configure(DbContextOptionsBuilder builder, string connection) =>
        builder.UseNpgsql(connection, options =>
        {
            options.MapEnum<SchoolStatus>("sm_status", nameTranslator: Names);
            options.MapEnum<SchoolRole>("sm_role", nameTranslator: Names);
            options.MapEnum<Permission>("sm_permission", nameTranslator: Names);
            options.MapEnum<EnrollmentStatus>("sm_enrollment_status", nameTranslator: Names);
            options.MapEnum<OperationStatus>("sm_operation_status", nameTranslator: Names);
            options.MapEnum<OperationChannel>("sm_channel", nameTranslator: Names);
            options.MapEnum<InvitationStatus>("sm_invitation_status", nameTranslator: Names);
        });
}
