using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SalesApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreditOverrideStatus",
                schema: "sal",
                table: "SalesOrders",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOverrideStatus",
                schema: "sal",
                table: "SalesOrders",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreditOverrideStatus",
                schema: "sal",
                table: "Invoices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountOverrideStatus",
                schema: "sal",
                table: "Invoices",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "sal",
                columns: table => new
                {
                    SalesApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverEmployeeId = table.Column<long>(type: "bigint", nullable: true),
                    RoleId = table.Column<int>(type: "integer", nullable: true),
                    StepStatus = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ActedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCommentRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.SalesApprovalStepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_Request",
                schema: "sal",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "DocumentType", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-102).
            migrationBuilder.Sql("""
                ALTER TABLE sal."ApprovalSteps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sal."ApprovalSteps" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS approvalsteps_tenant_isolation ON sal."ApprovalSteps";
                CREATE POLICY approvalsteps_tenant_isolation
                    ON sal."ApprovalSteps"
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
                name: "ApprovalSteps",
                schema: "sal");

            migrationBuilder.DropColumn(
                name: "CreditOverrideStatus",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideStatus",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CreditOverrideStatus",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountOverrideStatus",
                schema: "sal",
                table: "Invoices");
        }
    }
}
