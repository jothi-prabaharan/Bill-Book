using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberingSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumberingSeries",
                schema: "acc",
                columns: table => new
                {
                    NumberingSeriesId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SeriesSystemName = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    SeriesCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SeriesName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SeriesFor = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Prefix = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Suffix = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Separator = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    IncludeFinancialYear = table.Column<bool>(type: "boolean", nullable: false),
                    FinancialYearFormat = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    IncludeBranchCode = table.Column<bool>(type: "boolean", nullable: false),
                    BranchCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NumberLength = table.Column<int>(type: "integer", nullable: false),
                    StartNumber = table.Column<long>(type: "bigint", nullable: false),
                    NextNumber = table.Column<long>(type: "bigint", nullable: false),
                    ResetFrequency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LastResetOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AllowManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSeries", x => x.NumberingSeriesId);
                    table.CheckConstraint("chk_numbering_branch_code", "\"IncludeBranchCode\" = false OR \"BranchCode\" IS NOT NULL");
                    table.CheckConstraint("chk_numbering_counter", "\"NextNumber\" >= \"StartNumber\" AND \"NumberLength\" BETWEEN 1 AND 12");
                    table.CheckConstraint("chk_numbering_manual_override", "\"SeriesFor\" = 'Master' OR \"AllowManualOverride\" = false");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_OrgId",
                schema: "acc",
                table: "NumberingSeries",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_OrgId_SeriesName",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Order",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "DisplayOrder", "SeriesName" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Lookup",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_Default",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesCode", "BranchId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_SystemName",
                schema: "acc",
                table: "NumberingSeries",
                columns: new[] { "OrgId", "SeriesSystemName" },
                unique: true,
                filter: "\"SeriesSystemName\" IS NOT NULL");

            // Row-level security. The EF query filter is the first line of
            // defence; this is the one that holds if a query ever runs without
            // it. One of the four raw-SQL exceptions CLAUDE.md allows.
            //
            // Note this is the first acc table to carry a policy — Accounts,
            // SubAccounts and TaxMasters were created without one and are
            // relying on the query filter alone.
            migrationBuilder.Sql(
                "ALTER TABLE acc.\"NumberingSeries\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "CREATE POLICY numbering_series_org_isolation ON acc.\"NumberingSeries\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS numbering_series_org_isolation ON acc.\"NumberingSeries\";");

            migrationBuilder.DropTable(
                name: "NumberingSeries",
                schema: "acc");
        }
    }
}
