using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.Kernel.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RecurringBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringBillSchedules",
                schema: "pur",
                columns: table => new
                {
                    RecurringBillScheduleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateBillId = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Every = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    NextOccurrenceIndex = table.Column<int>(type: "integer", nullable: false),
                    NextRunDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OccurrencesCreated = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    AutoPost = table.Column<bool>(type: "boolean", nullable: false),
                    VendorBillNoPrefix = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlaceOfSupplyStateCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RecurringBillSchedules", x => x.RecurringBillScheduleId);
                    table.ForeignKey(
                        name: "FK_RecurringBillSchedules_Bills_TemplateBillId",
                        column: x => x.TemplateBillId,
                        principalSchema: "pur",
                        principalTable: "Bills",
                        principalColumn: "BillId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecurringBillRuns",
                schema: "pur",
                columns: table => new
                {
                    RecurringBillRunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecurringBillScheduleId = table.Column<long>(type: "bigint", nullable: false),
                    OccurrenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OccurrenceIndex = table.Column<int>(type: "integer", nullable: false),
                    BillId = table.Column<long>(type: "bigint", nullable: false),
                    Posted = table.Column<bool>(type: "boolean", nullable: false),
                    PostError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_RecurringBillRuns", x => x.RecurringBillRunId);
                    table.ForeignKey(
                        name: "FK_RecurringBillRuns_Bills_BillId",
                        column: x => x.BillId,
                        principalSchema: "pur",
                        principalTable: "Bills",
                        principalColumn: "BillId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecurringBillRuns_RecurringBillSchedules_RecurringBillSched~",
                        column: x => x.RecurringBillScheduleId,
                        principalSchema: "pur",
                        principalTable: "RecurringBillSchedules",
                        principalColumn: "RecurringBillScheduleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillRuns_BillId",
                schema: "pur",
                table: "RecurringBillRuns",
                column: "BillId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillRuns_CustomerId_OrgId",
                schema: "pur",
                table: "RecurringBillRuns",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillRuns_OrgId_RecurringBillScheduleId_OccurrenceD~",
                schema: "pur",
                table: "RecurringBillRuns",
                columns: new[] { "OrgId", "RecurringBillScheduleId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillRuns_RecurringBillScheduleId",
                schema: "pur",
                table: "RecurringBillRuns",
                column: "RecurringBillScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillSchedules_CustomerId_OrgId",
                schema: "pur",
                table: "RecurringBillSchedules",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillSchedules_OrgId_Status_NextRunDate",
                schema: "pur",
                table: "RecurringBillSchedules",
                columns: new[] { "OrgId", "Status", "NextRunDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringBillSchedules_TemplateBillId",
                schema: "pur",
                table: "RecurringBillSchedules",
                column: "TemplateBillId");

            // RLS in TK-71's form, after the tables exist (TK-139).
            TenantRlsMigration.Enable(migrationBuilder, "pur", "RecurringBillSchedules");
            TenantRlsMigration.Enable(migrationBuilder, "pur", "RecurringBillRuns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringBillRuns",
                schema: "pur");

            migrationBuilder.DropTable(
                name: "RecurringBillSchedules",
                schema: "pur");
        }
    }
}
