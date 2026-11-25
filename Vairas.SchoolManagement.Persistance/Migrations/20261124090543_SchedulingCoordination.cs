using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vairas.SchoolManagement.Persistance.Migrations
{
    /// <inheritdoc />
    public partial class SchedulingCoordination : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "school_coordination",
                columns: table => new
                {
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    school_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    command_json = table.Column<string>(type: "text", nullable: false),
                    transition_json = table.Column<string>(type: "text", nullable: false),
                    payload_hash = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    before_revision = table.Column<long>(type: "bigint", nullable: false),
                    after_revision = table.Column<long>(type: "bigint", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_school_coordination", x => new { x.operation_id, x.school_id });
                });
            var assembly=typeof(SchedulingCoordination).Assembly;
            using var stream=assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n=>n.EndsWith(".Sql.Coordination.sql",StringComparison.Ordinal)))!;
            using var reader=new StreamReader(stream);migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)=>throw new NotSupportedException("Coordination evidence is retained.");
    }
}
