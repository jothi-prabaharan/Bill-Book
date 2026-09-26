using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class StockAdjustmentApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "inv",
                table: "StockAdjustments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "inv",
                table: "StockAdjustments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "inv",
                table: "StockAdjustments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "inv",
                table: "StockAdjustments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "inv",
                columns: table => new
                {
                    InventoryApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Round = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ApprovalSteps", x => x.InventoryApprovalStepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_RequestKind_RequestId_Round_Sequence",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "inv",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-102).
            migrationBuilder.Sql("""
                ALTER TABLE inv."ApprovalSteps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE inv."ApprovalSteps" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS approvalsteps_tenant_isolation ON inv."ApprovalSteps";
                CREATE POLICY approvalsteps_tenant_isolation
                    ON inv."ApprovalSteps"
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
                schema: "inv");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "inv",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "inv",
                table: "StockAdjustments");
        }
    }
}
