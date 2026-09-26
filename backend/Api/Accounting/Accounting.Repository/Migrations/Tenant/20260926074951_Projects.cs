using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Projects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "acc",
                table: "JournalLedger",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "acc",
                table: "JournalDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "acc",
                columns: table => new
                {
                    ProjectId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    BillingMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RateBasis = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    FixedFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BudgetAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
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
                    table.PrimaryKey("PK_Projects", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                schema: "acc",
                columns: table => new
                {
                    ProjectMemberId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CostRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_ProjectMembers", x => x.ProjectMemberId);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "acc",
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                schema: "acc",
                columns: table => new
                {
                    ProjectMilestoneId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ProjectMilestones", x => x.ProjectMilestoneId);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "acc",
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTasks",
                schema: "acc",
                columns: table => new
                {
                    ProjectTaskId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<long>(type: "bigint", nullable: false),
                    TaskName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    HourlyRate = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    BudgetHours = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ProjectTasks", x => x.ProjectTaskId);
                    table.ForeignKey(
                        name: "FK_ProjectTasks_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "acc",
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpendMoneyDetails_ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiveMoneyDetails_ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_ProjectId",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "ProjectId" },
                filter: "\"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_ProjectId",
                schema: "acc",
                table: "JournalLedger",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalDetails_ProjectId",
                schema: "acc",
                table: "JournalDetails",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_CustomerId_OrgId",
                schema: "acc",
                table: "ProjectMembers",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_OrgId_ProjectId_UserId",
                schema: "acc",
                table: "ProjectMembers",
                columns: new[] { "OrgId", "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId",
                schema: "acc",
                table: "ProjectMembers",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_CustomerId_OrgId",
                schema: "acc",
                table: "ProjectMilestones",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_OrgId_ProjectId",
                schema: "acc",
                table: "ProjectMilestones",
                columns: new[] { "OrgId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId",
                schema: "acc",
                table: "ProjectMilestones",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_CustomerId_OrgId",
                schema: "acc",
                table: "ProjectTasks",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_OrgId_ProjectId",
                schema: "acc",
                table: "ProjectTasks",
                columns: new[] { "OrgId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTasks_ProjectId",
                schema: "acc",
                table: "ProjectTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerId_OrgId",
                schema: "acc",
                table: "Projects",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrgId_ContactId",
                schema: "acc",
                table: "Projects",
                columns: new[] { "OrgId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrgId_ProjectCode",
                schema: "acc",
                table: "Projects",
                columns: new[] { "OrgId", "ProjectCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrgId_Status",
                schema: "acc",
                table: "Projects",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_JournalDetails_Projects_ProjectId",
                schema: "acc",
                table: "JournalDetails",
                column: "ProjectId",
                principalSchema: "acc",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalLedger_Projects_ProjectId",
                schema: "acc",
                table: "JournalLedger",
                column: "ProjectId",
                principalSchema: "acc",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiveMoneyDetails_Projects_ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails",
                column: "ProjectId",
                principalSchema: "acc",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SpendMoneyDetails_Projects_ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails",
                column: "ProjectId",
                principalSchema: "acc",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            // RLS in TK-71's form on the four project tables: ENABLE, FORCE,
            // one policy on CustomerId and OrgId with NULLIF (TK-104).
            foreach (string table in new[] { "Projects", "ProjectTasks", "ProjectMembers", "ProjectMilestones" })
            {
                string policy = table.ToLowerInvariant() + "_tenant_isolation";
                migrationBuilder.Sql($"""
                    ALTER TABLE acc."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE acc."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON acc."{table}";
                    CREATE POLICY {policy}
                        ON acc."{table}"
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
            migrationBuilder.DropForeignKey(
                name: "FK_JournalDetails_Projects_ProjectId",
                schema: "acc",
                table: "JournalDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalLedger_Projects_ProjectId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiveMoneyDetails_Projects_ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_SpendMoneyDetails_Projects_ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropTable(
                name: "ProjectMembers",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ProjectMilestones",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "ProjectTasks",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "acc");

            migrationBuilder.DropIndex(
                name: "IX_SpendMoneyDetails_ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_ReceiveMoneyDetails_ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropIndex(
                name: "IX_JournalLedger_OrgId_ProjectId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropIndex(
                name: "IX_JournalLedger_ProjectId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropIndex(
                name: "IX_JournalDetails_ProjectId",
                schema: "acc",
                table: "JournalDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "acc",
                table: "SpendMoneyDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "acc",
                table: "ReceiveMoneyDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "acc",
                table: "JournalDetails");
        }
    }
}
