using Microsoft.EntityFrameworkCore.Migrations;
namespace Vairas.SchoolManagement.Persistance.Migrations;

public partial class InitialSchoolManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "Initial.sql", "Constraints.sql", "Hardening.sql" })
        {
            var assembly = typeof(InitialSchoolManagement).Assembly;
            using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Sql." + name, StringComparison.Ordinal)))!;
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }
    }
    protected override void Down(MigrationBuilder migrationBuilder) =>
     throw new NotSupportedException("School history cannot be destroyed by an automatic downgrade. Restore an explicitly approved backup.");
}
