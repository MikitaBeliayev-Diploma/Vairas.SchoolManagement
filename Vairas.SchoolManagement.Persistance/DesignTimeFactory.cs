using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Vairas.SchoolManagement.Persistance;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SchoolDbContext>().UseSchool(Environment.GetEnvironmentVariable("VAIRAS_MIGRATION_CONNECTION") ?? "Host=localhost;Database=vairas_school_management;Username=v_school_management_migration").Options);
}