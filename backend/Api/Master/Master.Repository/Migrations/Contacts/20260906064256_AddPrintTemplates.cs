using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Contacts
{
    /// <inheritdoc />
    public partial class AddPrintTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintTemplates",
                schema: "con",
                columns: table => new
                {
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentTypeCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    SeedVersion = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_PrintTemplates", x => x.PrintTemplateId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_CustomerId_OrgId",
                schema: "con",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_Default",
                schema: "con",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId", "DocumentTypeCode" },
                unique: true,
                filter: "\"IsDefault\" AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_Name",
                schema: "con",
                table: "PrintTemplates",
                columns: new[] { "CustomerId", "OrgId", "DocumentTypeCode", "TemplateName" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_PrintTemplates_OrgId_DocumentTypeCode_IsActive",
                schema: "con",
                table: "PrintTemplates",
                columns: new[] { "OrgId", "DocumentTypeCode", "IsActive" });

            // Row-level security, the same shape every other con table carries.
            // The query filter is the first guard and this is the independent
            // second one: it holds even for a query that reached the database
            // through IgnoreQueryFilters.
            migrationBuilder.Sql("ALTER TABLE con.\"PrintTemplates\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE con.\"PrintTemplates\" FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY printtemplates_tenant_isolation ON con.\"PrintTemplates\" " +
                "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintTemplates",
                schema: "con");
        }
    }
}
