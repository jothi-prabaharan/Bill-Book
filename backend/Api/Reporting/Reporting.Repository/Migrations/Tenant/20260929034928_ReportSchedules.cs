using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ReportSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Scheduled report email (TK-114).
            migrationBuilder.CreateTable(
                name: "ReportSchedules",
                schema: "rpt",
                columns: table => new
                {
                    ReportScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CustomReportId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LayoutJson = table.Column<string>(type: "jsonb", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DayOfWeek = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    SendAtHour = table.Column<int>(type: "integer", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserIds = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_ReportSchedules", x => x.ReportScheduleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_CustomerId_OrgId",
                schema: "rpt",
                table: "ReportSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Due",
                schema: "rpt",
                table: "ReportSchedules",
                column: "NextRunAt",
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Owner",
                schema: "rpt",
                table: "ReportSchedules",
                columns: new[] { "OrgId", "OwnerUserId" });

            // TK-71's policy, as every rpt table carrying CustomerId and OrgId has it.
            migrationBuilder.Sql("""
                ALTER TABLE rpt."ReportSchedules" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE rpt."ReportSchedules" FORCE ROW LEVEL SECURITY;
                CREATE POLICY reportschedules_tenant_isolation
                    ON rpt."ReportSchedules"
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
                name: "ReportSchedules",
                schema: "rpt");
        }
    }
}
