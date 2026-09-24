using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Hrm.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                schema: "hrm",
                columns: table => new
                {
                    ChecklistTemplateId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.ChecklistTemplateId);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeChecklists",
                schema: "hrm",
                columns: table => new
                {
                    EmployeeChecklistId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChecklistTemplateId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_EmployeeChecklists", x => x.EmployeeChecklistId);
                });

            migrationBuilder.CreateTable(
                name: "Separations",
                schema: "hrm",
                columns: table => new
                {
                    SeparationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastWorkingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NoticeShortfallDays = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsNoticeWaived = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExitInterviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_Separations", x => x.SeparationId);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplateItems",
                schema: "hrm",
                columns: table => new
                {
                    ChecklistTemplateItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChecklistTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ChecklistTemplateItems", x => x.ChecklistTemplateItemId);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplateItems_ChecklistTemplates_ChecklistTemplate~",
                        column: x => x.ChecklistTemplateId,
                        principalSchema: "hrm",
                        principalTable: "ChecklistTemplates",
                        principalColumn: "ChecklistTemplateId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeChecklistItems",
                schema: "hrm",
                columns: table => new
                {
                    EmployeeChecklistItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeChecklistId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    DoneDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeChecklistItems", x => x.EmployeeChecklistItemId);
                    table.ForeignKey(
                        name: "FK_EmployeeChecklistItems_EmployeeChecklists_EmployeeChecklist~",
                        column: x => x.EmployeeChecklistId,
                        principalSchema: "hrm",
                        principalTable: "EmployeeChecklists",
                        principalColumn: "EmployeeChecklistId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateItems_ChecklistTemplateId",
                schema: "hrm",
                table: "ChecklistTemplateItems",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplateItems_CustomerId_OrgId",
                schema: "hrm",
                table: "ChecklistTemplateItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_CustomerId_OrgId",
                schema: "hrm",
                table: "ChecklistTemplates",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChecklistItems_CustomerId_OrgId",
                schema: "hrm",
                table: "EmployeeChecklistItems",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChecklistItems_EmployeeChecklistId",
                schema: "hrm",
                table: "EmployeeChecklistItems",
                column: "EmployeeChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChecklists_CustomerId_OrgId",
                schema: "hrm",
                table: "EmployeeChecklists",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Separations_CustomerId_OrgId",
                schema: "hrm",
                table: "Separations",
                columns: new[] { "CustomerId", "OrgId" });

            string[] rlsTables =
            [
                "ChecklistTemplates",
                "ChecklistTemplateItems",
                "EmployeeChecklists",
                "EmployeeChecklistItems",
                "Separations"
            ];

            foreach (string table in rlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE hrm."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE hrm."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON hrm."{table}";
                    CREATE POLICY {policy}
                        ON hrm."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistTemplateItems",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "EmployeeChecklistItems",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "Separations",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates",
                schema: "hrm");

            migrationBuilder.DropTable(
                name: "EmployeeChecklists",
                schema: "hrm");
        }
    }
}
