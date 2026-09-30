using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddMarketingAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomationEnrolments",
                schema: "cus",
                columns: table => new
                {
                    AutomationEnrolmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutomationId = table.Column<long>(type: "bigint", nullable: false),
                    LeadId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CurrentStep = table.Column<int>(type: "integer", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExitReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_AutomationEnrolments", x => x.AutomationEnrolmentId);
                });

            migrationBuilder.CreateTable(
                name: "Automations",
                schema: "cus",
                columns: table => new
                {
                    AutomationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TriggerSource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TriggerStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_Automations", x => x.AutomationId);
                });

            migrationBuilder.CreateTable(
                name: "AutomationSteps",
                schema: "cus",
                columns: table => new
                {
                    AutomationStepId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutomationId = table.Column<long>(type: "bigint", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WaitDays = table.Column<int>(type: "integer", nullable: false),
                    EmailTemplateId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_AutomationSteps", x => x.AutomationStepId);
                    table.ForeignKey(
                        name: "FK_AutomationSteps_Automations_AutomationId",
                        column: x => x.AutomationId,
                        principalSchema: "cus",
                        principalTable: "Automations",
                        principalColumn: "AutomationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_CustomerId_OrgId",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_OrgId_AutomationId_Email_Status",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "OrgId", "AutomationId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationEnrolments_OrgId_Status_NextRunAt",
                schema: "cus",
                table: "AutomationEnrolments",
                columns: new[] { "OrgId", "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_AutomationId",
                schema: "cus",
                table: "AutomationSteps",
                column: "AutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_CustomerId_OrgId",
                schema: "cus",
                table: "AutomationSteps",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationSteps_OrgId_AutomationId_Sequence",
                schema: "cus",
                table: "AutomationSteps",
                columns: new[] { "OrgId", "AutomationId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_CustomerId_OrgId",
                schema: "cus",
                table: "Automations",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Automations_OrgId_IsActive_TriggerType",
                schema: "cus",
                table: "Automations",
                columns: new[] { "OrgId", "IsActive", "TriggerType" });

            migrationBuilder.Sql("""
                ALTER TABLE cus."Automations" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."Automations" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS automations_tenant_isolation ON cus."Automations";
                CREATE POLICY automations_tenant_isolation
                    ON cus."Automations"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."AutomationSteps" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."AutomationSteps" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS automationsteps_tenant_isolation ON cus."AutomationSteps";
                CREATE POLICY automationsteps_tenant_isolation
                    ON cus."AutomationSteps"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);

                ALTER TABLE cus."AutomationEnrolments" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE cus."AutomationEnrolments" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS automationenrolments_tenant_isolation ON cus."AutomationEnrolments";
                CREATE POLICY automationenrolments_tenant_isolation
                    ON cus."AutomationEnrolments"
                    FOR ALL
                    USING (
                        "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                        AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY IF EXISTS automations_tenant_isolation ON cus."Automations";
                DROP POLICY IF EXISTS automationsteps_tenant_isolation ON cus."AutomationSteps";
                DROP POLICY IF EXISTS automationenrolments_tenant_isolation ON cus."AutomationEnrolments";
                """);

            migrationBuilder.DropTable(
                name: "AutomationEnrolments",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "AutomationSteps",
                schema: "cus");

            migrationBuilder.DropTable(
                name: "Automations",
                schema: "cus");
        }
    }
}
