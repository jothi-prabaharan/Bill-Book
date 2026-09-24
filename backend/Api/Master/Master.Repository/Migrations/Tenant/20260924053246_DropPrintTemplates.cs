using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <summary>
    /// Print templates moved to Printing's own <c>prt.PrintTemplates</c> (TK-24).
    ///
    /// <b>Dropped, not copied.</b> Nothing was deployed (D-13), and the copy
    /// could only have been a raw INSERT … SELECT across two services' schemas,
    /// outside hard rule 1's exceptions. Branches are re-seeded in <c>prt</c>
    /// instead (owner, 2026-09-24). A document's old <c>PrintTemplateId</c> that
    /// no longer resolves falls back to the branch default, then to the
    /// standard layout, so nothing stops printing.
    ///
    /// Down recreates the table empty and without its RLS policy; the policy
    /// belongs to <c>EnableRowLevelSecurity</c>, and the rows are gone either way.
    /// </summary>
    public partial class DropPrintTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintTemplates",
                schema: "con");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintTemplates",
                schema: "con",
                columns: table => new
                {
                    PrintTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentTypeCode = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeedVersion = table.Column<int>(type: "integer", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateVersion = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
        }
    }
}
