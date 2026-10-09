using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class CustomReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Custom reports (TK-113). The read models' CustomFields and the
            // invoice's currency columns belong to Sales, Purchase, Master and
            // Inventory, so they change the snapshot only.
            migrationBuilder.CreateTable(
                name: "CustomReports",
                schema: "rpt",
                columns: table => new
                {
                    CustomReportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DatasetKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefinitionJson = table.Column<string>(type: "jsonb", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomReports", x => x.CustomReportId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomReports_CustomerId_OrgId",
                schema: "rpt",
                table: "CustomReports",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomReports_Dataset",
                schema: "rpt",
                table: "CustomReports",
                columns: new[] { "OrgId", "DatasetKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomReports_Title",
                schema: "rpt",
                table: "CustomReports",
                columns: new[] { "OrgId", "OwnerUserId", "Title" },
                unique: true,
                filter: "\"IsActive\" = true")
                .Annotation("Npgsql:NullsDistinct", false);

            // TK-71's policy, as every rpt table carrying CustomerId and OrgId
            // has it: see 20260924061423_EnableRowLevelSecurity.
            migrationBuilder.Sql("""
                ALTER TABLE rpt."CustomReports" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE rpt."CustomReports" FORCE ROW LEVEL SECURITY;
                CREATE POLICY customreports_tenant_isolation
                    ON rpt."CustomReports"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomReports",
                schema: "rpt");
        }
    }
}
