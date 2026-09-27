using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ProjectBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BilledInvoiceId",
                schema: "acc",
                table: "SpendMoneyDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                schema: "acc",
                table: "SpendMoneyDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupPercent",
                schema: "acc",
                table: "SpendMoneyDetails",
                type: "numeric(7,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                schema: "acc",
                columns: table => new
                {
                    TimeEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectTaskId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    BillingStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TimeEntries", x => x.TimeEntryId);
                    table.CheckConstraint("chk_timeentries_hours", "\"Hours\" > 0 AND \"Hours\" <= 24");
                    table.ForeignKey(
                        name: "FK_TimeEntries_ProjectTasks_ProjectTaskId",
                        column: x => x.ProjectTaskId,
                        principalSchema: "acc",
                        principalTable: "ProjectTasks",
                        principalColumn: "ProjectTaskId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimeEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "acc",
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_OrgId_BilledInvoiceId",
                schema: "acc",
                table: "SpendMoneyDetails",
                columns: new[] { "OrgId", "BilledInvoiceId" },
                filter: "\"BilledInvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_CustomerId_OrgId",
                schema: "acc",
                table: "TimeEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_OrgId_InvoiceId",
                schema: "acc",
                table: "TimeEntries",
                columns: new[] { "OrgId", "InvoiceId" },
                filter: "\"InvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_OrgId_ProjectId_BillingStatus",
                schema: "acc",
                table: "TimeEntries",
                columns: new[] { "OrgId", "ProjectId", "BillingStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_OrgId_UserId_WorkDate",
                schema: "acc",
                table: "TimeEntries",
                columns: new[] { "OrgId", "UserId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ProjectId",
                schema: "acc",
                table: "TimeEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_ProjectTaskId",
                schema: "acc",
                table: "TimeEntries",
                column: "ProjectTaskId");

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-106).
            migrationBuilder.Sql("""
                ALTER TABLE acc."TimeEntries" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acc."TimeEntries" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS timeentries_tenant_isolation ON acc."TimeEntries";
                CREATE POLICY timeentries_tenant_isolation
                    ON acc."TimeEntries"
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
                name: "TimeEntries",
                schema: "acc");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoneyDetails_OrgId_BilledInvoiceId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropColumn(
                name: "BilledInvoiceId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropColumn(
                name: "MarkupPercent",
                schema: "acc",
                table: "SpendMoneyDetails");
        }
    }
}
