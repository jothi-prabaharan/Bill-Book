using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Site.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteAttendanceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LaborerClockEntries",
                schema: "sit");

            migrationBuilder.CreateTable(
                name: "SiteAttendance",
                schema: "sit",
                columns: table => new
                {
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    LaborerId = table.Column<long>(type: "bigint", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CheckInLat = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    CheckInLng = table.Column<decimal>(type: "numeric(10,7)", nullable: true),
                    IsGeofenceBreached = table.Column<bool>(type: "boolean", nullable: false),
                    ClockOutTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DayFraction = table.Column<decimal>(type: "numeric(3,2)", nullable: false),
                    OtHoursClaimed = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    OtHoursApproved = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    WorkSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalRequestId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DayCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DayMarginSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DaySellSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtCostSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtMarginSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtSellSnapshot = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SiteBillLineId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_SiteAttendance", x => x.AttendanceId);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_Laborers_LaborerId",
                        column: x => x.LaborerId,
                        principalSchema: "sit",
                        principalTable: "Laborers",
                        principalColumn: "LaborerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_SiteAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "sit",
                        principalTable: "SiteAssignments",
                        principalColumn: "SiteAssignmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiteAttendance_Sites_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "sit",
                        principalTable: "Sites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteAttendancePhotos",
                schema: "sit",
                columns: table => new
                {
                    PhotoId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_SiteAttendancePhotos", x => x.PhotoId);
                    table.ForeignKey(
                        name: "FK_SiteAttendancePhotos_SiteAttendance_AttendanceId",
                        column: x => x.AttendanceId,
                        principalSchema: "sit",
                        principalTable: "SiteAttendance",
                        principalColumn: "AttendanceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_AssignmentId",
                schema: "sit",
                table: "SiteAttendance",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_AssignmentId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "AssignmentId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_LaborerId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "LaborerId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_SiteId_WorkDate",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_CustomerId_OrgId_Status",
                schema: "sit",
                table: "SiteAttendance",
                columns: new[] { "CustomerId", "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_LaborerId",
                schema: "sit",
                table: "SiteAttendance",
                column: "LaborerId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendance_SiteId",
                schema: "sit",
                table: "SiteAttendance",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_AttendanceId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_CustomerId_OrgId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SiteAttendancePhotos_CustomerId_OrgId_AttendanceId",
                schema: "sit",
                table: "SiteAttendancePhotos",
                columns: new[] { "CustomerId", "OrgId", "AttendanceId" });

            var tables = new[]
            {
                "SiteAttendance",
                "SiteAttendancePhotos",
            };

            foreach (var table in tables)
            {
                var policy = $"\"sit_{table.ToLowerInvariant()}_tenant_isolation\"";
                migrationBuilder.Sql($"""
                    ALTER TABLE sit."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sit."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sit."{table}";
                    CREATE POLICY {policy}
                        ON sit."{table}"
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
                name: "SiteAttendancePhotos",
                schema: "sit");

            migrationBuilder.DropTable(
                name: "SiteAttendance",
                schema: "sit");

            migrationBuilder.CreateTable(
                name: "LaborerClockEntries",
                schema: "sit",
                columns: table => new
                {
                    LaborerClockEntryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApprovalStatus = table.Column<int>(type: "integer", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClockInLatitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockInLongitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockInUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ClockOutLatitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockOutLongitude = table.Column<decimal>(type: "numeric(9,6)", nullable: true),
                    ClockOutUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsOtClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    LaborerId = table.Column<long>(type: "bigint", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OvertimeHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    RegularHours = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SelfiePhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SiteId = table.Column<long>(type: "bigint", nullable: false),
                    SitePhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaborerClockEntries", x => x.LaborerClockEntryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaborerClockEntries_CustomerId_OrgId",
                schema: "sit",
                table: "LaborerClockEntries",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_LaborerClockEntries_CustomerId_OrgId_SiteId_LaborerId_Clock~",
                schema: "sit",
                table: "LaborerClockEntries",
                columns: new[] { "CustomerId", "OrgId", "SiteId", "LaborerId", "ClockInUtc" });
        }
    }
}
