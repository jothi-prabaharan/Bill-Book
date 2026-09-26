using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AccountingApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "acc",
                table: "SpendMoney",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "acc",
                table: "SpendMoney",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "acc",
                table: "SpendMoney",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                schema: "acc",
                table: "Journals",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApproverRoleId",
                schema: "acc",
                table: "Journals",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentApproverUserId",
                schema: "acc",
                table: "Journals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepLabel",
                schema: "acc",
                table: "Journals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                schema: "acc",
                columns: table => new
                {
                    AccountingApprovalStepId = table.Column<long>(type: "bigint", nullable: false)
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
                    table.PrimaryKey("PK_ApprovalSteps", x => x.AccountingApprovalStepId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_CustomerId_OrgId",
                schema: "acc",
                table: "ApprovalSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_RequestKind_RequestId_Round_Sequence",
                schema: "acc",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "RequestKind", "RequestId", "Round", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_ApproverUserId",
                schema: "acc",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_OrgId_StepStatus_RoleId",
                schema: "acc",
                table: "ApprovalSteps",
                columns: new[] { "OrgId", "StepStatus", "RoleId" });

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-101).
            migrationBuilder.Sql("""
                ALTER TABLE acc."ApprovalSteps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acc."ApprovalSteps" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS approvalsteps_tenant_isolation ON acc."ApprovalSteps";
                CREATE POLICY approvalsteps_tenant_isolation
                    ON acc."ApprovalSteps"
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
                schema: "acc");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropColumn(
                name: "CurrentApproverRoleId",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropColumn(
                name: "CurrentApproverUserId",
                schema: "acc",
                table: "Journals");

            migrationBuilder.DropColumn(
                name: "CurrentStepLabel",
                schema: "acc",
                table: "Journals");
        }
    }
}
