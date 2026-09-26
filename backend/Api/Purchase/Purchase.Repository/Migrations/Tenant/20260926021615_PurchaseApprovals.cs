using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PurchaseApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "pur",
                table: "PurchaseOrders",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "PurchaseOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "PurchaseOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "pur",
                table: "GoodsReceipts",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "GoodsReceipts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "GoodsReceipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "GoodsReceipts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "pur",
                table: "DebitNotes",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "DebitNotes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "DebitNotes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "DebitNotes",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "pur",
                table: "Bills",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "Bills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "Bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "Bills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "pur",
                columns: table => new
                {
                    PurchaseApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
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
                    table.PrimaryKey("PK_ApprovalSteps", x => x.PurchaseApprovalStepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "pur",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_RequestKind_RequestId_Round_Sequence",
                schema: "pur",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "pur",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "pur",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-100).
            migrationBuilder.Sql("""
                ALTER TABLE pur."ApprovalSteps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pur."ApprovalSteps" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS approvalsteps_tenant_isolation ON pur."ApprovalSteps";
                CREATE POLICY approvalsteps_tenant_isolation
                    ON pur."ApprovalSteps"
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
                schema: "pur");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "pur",
                table: "Bills");
        }
    }
}
