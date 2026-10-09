using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Budgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Budgets",
                schema: "acc",
                columns: table => new
                {
                    BudgetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FinancialYearStart = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    IsBranchDefault = table.Column<bool>(type: "boolean", nullable: false),
                    RevisionOf = table.Column<long>(type: "bigint", nullable: true),
                    RevisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ByProject = table.Column<bool>(type: "boolean", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_Budgets", x => x.BudgetId);
                    table.CheckConstraint("chk_budgets_default_approved", "NOT \"IsBranchDefault\" OR \"Status\" = 'Approved'");
                    table.ForeignKey(
                        name: "FK_Budgets_Budgets_RevisionOf",
                        column: x => x.RevisionOf,
                        principalSchema: "acc",
                        principalTable: "Budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                schema: "acc",
                columns: table => new
                {
                    BudgetLineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BudgetId = table.Column<long>(type: "bigint", nullable: false),
                    AccountId = table.Column<long>(type: "bigint", nullable: false),
                    ProjectId = table.Column<long>(type: "bigint", nullable: true),
                    Period = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
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
                    table.PrimaryKey("PK_BudgetLines", x => x.BudgetLineId);
                    table.CheckConstraint("chk_budgetlines_amount", "\"Amount\" >= 0");
                    table.CheckConstraint("chk_budgetlines_period", "\"Period\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_BudgetLines_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "AccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalSchema: "acc",
                        principalTable: "Budgets",
                        principalColumn: "BudgetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BudgetLines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "acc",
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_AccountId",
                schema: "acc",
                table: "BudgetLines",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_BudgetId_AccountId_ProjectId_Period",
                schema: "acc",
                table: "BudgetLines",
                columns: new[] { "BudgetId", "AccountId", "ProjectId", "Period" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_CustomerId_OrgId",
                schema: "acc",
                table: "BudgetLines",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_ProjectId",
                schema: "acc",
                table: "BudgetLines",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_BranchDefault",
                schema: "acc",
                table: "Budgets",
                columns: new[] { "CustomerId", "OrgId", "FinancialYearStart" },
                unique: true,
                filter: "\"IsBranchDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_CustomerId_OrgId",
                schema: "acc",
                table: "Budgets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_OrgId_FinancialYearStart_Status",
                schema: "acc",
                table: "Budgets",
                columns: new[] { "OrgId", "FinancialYearStart", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_RevisionOf",
                schema: "acc",
                table: "Budgets",
                column: "RevisionOf");

            // RLS in TK-71's form: ENABLE, FORCE, one policy on CustomerId and
            // OrgId with NULLIF (TK-108).
            migrationBuilder.Sql("""
                ALTER TABLE acc."Budgets" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acc."Budgets" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS budgets_tenant_isolation ON acc."Budgets";
                CREATE POLICY budgets_tenant_isolation
                    ON acc."Budgets"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
            migrationBuilder.Sql("""
                ALTER TABLE acc."BudgetLines" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE acc."BudgetLines" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS budgetlines_tenant_isolation ON acc."BudgetLines";
                CREATE POLICY budgetlines_tenant_isolation
                    ON acc."BudgetLines"
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
                name: "BudgetLines",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Budgets",
                schema: "acc");
        }
    }
}
