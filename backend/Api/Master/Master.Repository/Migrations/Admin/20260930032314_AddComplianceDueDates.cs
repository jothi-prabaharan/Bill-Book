using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AddComplianceDueDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceDueDates",
                schema: "mst",
                columns: table => new
                {
                    ComplianceDueDateId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FormName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    DueDay = table.Column<int>(type: "integer", nullable: false),
                    DueMonthOffset = table.Column<int>(type: "integer", nullable: false),
                    RuleDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApplicabilityCondition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SpecificPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExtendedDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExtensionNotice = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceDueDates", x => x.ComplianceDueDateId);
                });

            migrationBuilder.CreateTable(
                name: "OrgComplianceSettings",
                schema: "mst",
                columns: table => new
                {
                    OrgComplianceSettingId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComplianceDueDateId = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    ReminderDaysBefore = table.Column<int>(type: "integer", nullable: false),
                    LastFiledPeriod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastFiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgComplianceSettings", x => x.OrgComplianceSettingId);
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "ComplianceDueDates",
                columns: new[] { "ComplianceDueDateId", "ApplicabilityCondition", "Category", "CreatedAt", "CreatedBy", "Description", "DueDay", "DueMonthOffset", "ExtendedDueDate", "ExtensionNotice", "FormName", "Frequency", "IsActive", "ModifiedAt", "ModifiedBy", "RuleDescription", "SpecificPeriod" },
                values: new object[,]
                {
                    { 1, "GSTIN", "GST", null, null, "Outward supplies monthly return", 11, 1, null, null, "GSTR-1", 1, true, null, null, "11th of the following month", null },
                    { 2, "GSTIN", "GST", null, null, "Monthly summary return and tax payment", 20, 1, null, null, "GSTR-3B", 1, true, null, null, "20th of the following month", null },
                    { 3, "TAN", "TDS", null, null, "Monthly tax deducted at source deposit (Challan 281)", 7, 1, null, null, "TDS Deposit", 1, true, null, null, "7th of the following month (30th April for March)", null },
                    { 4, "TAN", "TDS", null, null, "Quarterly TDS return for payments other than salary", 31, 1, null, null, "Form 26Q", 2, true, null, null, "31st of the month following quarter (31st May for Q4)", null },
                    { 5, "TAN", "TDS", null, null, "Quarterly TDS return for payments to non-residents", 31, 1, null, null, "Form 27Q", 2, true, null, null, "31st of the month following quarter (31st May for Q4)", null },
                    { 6, "All", "IncomeTax", null, null, "Quarterly advance income tax installment", 15, 0, null, null, "Advance Tax", 2, true, null, null, "15th of June, September, December, and March", null },
                    { 7, "CIN", "MSME", null, null, "Half-yearly return to MCA for outstanding payments to MSME vendors", 31, 1, null, null, "MSME Form 1", 3, true, null, null, "31st October (for Apr–Sep) and 30th April (for Oct–Mar)", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceDueDates_Category_FormName",
                schema: "mst",
                table: "ComplianceDueDates",
                columns: new[] { "Category", "FormName" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgComplianceSettings_OrgId_ComplianceDueDateId",
                schema: "mst",
                table: "OrgComplianceSettings",
                columns: new[] { "OrgId", "ComplianceDueDateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceDueDates",
                schema: "mst");

            migrationBuilder.DropTable(
                name: "OrgComplianceSettings",
                schema: "mst");
        }
    }
}
