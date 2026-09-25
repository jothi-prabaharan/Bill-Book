using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Amc.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialAmcSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "amc");

            migrationBuilder.CreateTable(
                name: "AmcContracts",
                schema: "amc",
                columns: table => new
                {
                    AmcContractId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    VendorContactId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractValue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    BillingFrequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VisitsPerYear = table.Column<int>(type: "integer", nullable: false),
                    AmcCoverage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RenewalReminderDays = table.Column<int>(type: "integer", nullable: false),
                    ReminderEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContractStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TerminationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AmcContracts", x => x.AmcContractId);
                    table.CheckConstraint("chk_amc_dates", "\"EndDate\" > \"StartDate\"");
                    table.CheckConstraint("chk_amc_terminated", "\"ContractStatus\" <> 'Terminated' OR \"TerminationReason\" IS NOT NULL");
                    table.CheckConstraint("chk_amc_value", "\"ContractValue\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ErrorLogs",
                schema: "amc",
                columns: table => new
                {
                    ErrorLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ErrorReference = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    SqlState = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    RequestMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    RequestPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    JobReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExceptionType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Hint = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Where = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Routine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConstraintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchemaName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TableName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StackTrace = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    InnerExceptions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FollowUpStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FollowUpNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ErrorLogs", x => x.ErrorLogId);
                });

            migrationBuilder.CreateTable(
                name: "AmcCoveredAssets",
                schema: "amc",
                columns: table => new
                {
                    AmcCoveredAssetId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AmcContractId = table.Column<long>(type: "bigint", nullable: false),
                    FacilityAssetId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_AmcCoveredAssets", x => x.AmcCoveredAssetId);
                    table.ForeignKey(
                        name: "FK_AmcCoveredAssets_AmcContracts_AmcContractId",
                        column: x => x.AmcContractId,
                        principalSchema: "amc",
                        principalTable: "AmcContracts",
                        principalColumn: "AmcContractId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AmcVisits",
                schema: "amc",
                columns: table => new
                {
                    AmcVisitId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AmcContractId = table.Column<long>(type: "bigint", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VisitKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FacilityAssetId = table.Column<long>(type: "bigint", nullable: true),
                    Remarks = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkOrderId = table.Column<long>(type: "bigint", nullable: true),
                    WorkOrderNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
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
                    table.PrimaryKey("PK_AmcVisits", x => x.AmcVisitId);
                    table.ForeignKey(
                        name: "FK_AmcVisits_AmcContracts_AmcContractId",
                        column: x => x.AmcContractId,
                        principalSchema: "amc",
                        principalTable: "AmcContracts",
                        principalColumn: "AmcContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_CustomerId_OrgId",
                schema: "amc",
                table: "AmcContracts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_CustomerId_OrgId_VendorContactId_ContractNo",
                schema: "amc",
                table: "AmcContracts",
                columns: new[] { "CustomerId", "OrgId", "VendorContactId", "ContractNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmcContracts_OrgId_ContractStatus_EndDate",
                schema: "amc",
                table: "AmcContracts",
                columns: new[] { "OrgId", "ContractStatus", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcCoveredAssets_AmcContractId",
                schema: "amc",
                table: "AmcCoveredAssets",
                column: "AmcContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AmcCoveredAssets_CustomerId_OrgId",
                schema: "amc",
                table: "AmcCoveredAssets",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcCoveredAssets_CustomerId_OrgId_AmcContractId_FacilityAss~",
                schema: "amc",
                table: "AmcCoveredAssets",
                columns: new[] { "CustomerId", "OrgId", "AmcContractId", "FacilityAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmcCoveredAssets_OrgId_FacilityAssetId",
                schema: "amc",
                table: "AmcCoveredAssets",
                columns: new[] { "OrgId", "FacilityAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcVisits_AmcContractId",
                schema: "amc",
                table: "AmcVisits",
                column: "AmcContractId");

            migrationBuilder.CreateIndex(
                name: "IX_AmcVisits_CustomerId_OrgId",
                schema: "amc",
                table: "AmcVisits",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_AmcVisits_OrgId_AmcContractId_VisitDate",
                schema: "amc",
                table: "AmcVisits",
                columns: new[] { "OrgId", "AmcContractId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_CustomerId_OrgId",
                schema: "amc",
                table: "ErrorLogs",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_ErrorReference",
                schema: "amc",
                table: "ErrorLogs",
                column: "ErrorReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErrorLogs_OrgId_FollowUpStatus_OccurredAt",
                schema: "amc",
                table: "ErrorLogs",
                columns: new[] { "OrgId", "FollowUpStatus", "OccurredAt" });

            // Row-level security on every amc table (S8, TK-68), in the same
            // migration that creates them, in TK-71's form: ENABLE, FORCE, one
            // policy on CustomerId and OrgId, and NULLIF so a request with no
            // tenant sees nothing rather than failing on ''::uuid. No WITH
            // CHECK, so USING is also the write check.
            foreach (string table in RlsTables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE amc."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE amc."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON amc."{table}";
                    CREATE POLICY {policy}
                        ON amc."{table}"
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
                name: "AmcCoveredAssets",
                schema: "amc");

            migrationBuilder.DropTable(
                name: "AmcVisits",
                schema: "amc");

            migrationBuilder.DropTable(
                name: "ErrorLogs",
                schema: "amc");

            migrationBuilder.DropTable(
                name: "AmcContracts",
                schema: "amc");
        }

        private static readonly string[] RlsTables =
        [
            "AmcContracts",
            "ErrorLogs",
            "AmcCoveredAssets",
            "AmcVisits",
        ];
    }
}
