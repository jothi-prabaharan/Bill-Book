using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeriodLocks",
                schema: "acc",
                columns: table => new
                {
                    PeriodLockId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    LockedUpto = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodLocks", x => x.PeriodLockId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_OrgId",
                schema: "acc",
                table: "PeriodLocks",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodLocks_OrgId_RoleId",
                schema: "acc",
                table: "PeriodLocks",
                columns: new[] { "OrgId", "RoleId" },
                unique: true);
            // Row-level security, as on every other per-customer table. A period
            // lock is a control on what may be written, so a branch reading
            // another branch's would be told the wrong dates are closed.
            migrationBuilder.Sql("ALTER TABLE acc.\"PeriodLocks\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS periodlocks_org_isolation ON acc.\"PeriodLocks\";");
            migrationBuilder.Sql(
                "CREATE POLICY periodlocks_org_isolation ON acc.\"PeriodLocks\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS periodlocks_org_isolation ON acc.\"PeriodLocks\";");

            migrationBuilder.DropTable(
                name: "PeriodLocks",
                schema: "acc");
        }
    }
}
