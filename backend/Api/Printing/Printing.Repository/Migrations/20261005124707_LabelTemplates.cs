using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Printing.Repository.Migrations
{
    /// <inheritdoc />
    public partial class LabelTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabelTemplates",
                schema: "prt",
                columns: table => new
                {
                    LabelTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WidthMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    HeightMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    GapMm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Columns = table.Column<int>(type: "integer", nullable: false),
                    ColumnGapMm = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_LabelTemplates", x => x.LabelTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_CustomerId_OrgId",
                schema: "prt",
                table: "LabelTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LabelTemplates_Default",
                schema: "prt",
                table: "LabelTemplates",
                columns: new[] { "CustomerId", "OrgId", "Kind" },
                unique: true,
                filter: "\"IsDefault\" AND \"IsActive\"");

            // Row-level security, as every other tenant table has it (TK-71's
            // template): enabled, FORCEd, one policy on CustomerId and OrgId.
            migrationBuilder.Sql("""
                ALTER TABLE prt."LabelTemplates" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE prt."LabelTemplates" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS labeltemplates_tenant_isolation ON prt."LabelTemplates";
                CREATE POLICY labeltemplates_tenant_isolation
                    ON prt."LabelTemplates"
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
                name: "LabelTemplates",
                schema: "prt");
        }
    }
}
