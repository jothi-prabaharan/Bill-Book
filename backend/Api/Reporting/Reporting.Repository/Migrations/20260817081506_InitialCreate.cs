using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Reporting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rpt");

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "rpt",
                columns: table => new
                {
                    ReportId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiredPermission = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.ReportId);
                });

            migrationBuilder.CreateTable(
                name: "ReportDetails",
                schema: "rpt",
                columns: table => new
                {
                    ReportDetailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<long>(type: "bigint", nullable: false),
                    ColumnKey = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Header = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DataType = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsFilterable = table.Column<bool>(type: "boolean", nullable: false),
                    IsSortable = table.Column<bool>(type: "boolean", nullable: false),
                    IsGroupable = table.Column<bool>(type: "boolean", nullable: false),
                    IsPivotable = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAggregate = table.Column<int>(type: "integer", nullable: false),
                    Alignment = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDetails", x => x.ReportDetailId);
                    table.ForeignKey(
                        name: "FK_ReportDetails_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "rpt",
                        principalTable: "Reports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportViews",
                schema: "rpt",
                columns: table => new
                {
                    ReportViewId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReportId = table.Column<long>(type: "bigint", nullable: false),
                    ViewName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    LayoutJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    OrgId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportViews", x => x.ReportViewId);
                    table.ForeignKey(
                        name: "FK_ReportViews_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "rpt",
                        principalTable: "Reports",
                        principalColumn: "ReportId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_Order",
                schema: "rpt",
                table: "ReportDetails",
                columns: new[] { "OrgId", "ReportId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_OrgId",
                schema: "rpt",
                table: "ReportDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_OrgId_ReportId_ColumnKey",
                schema: "rpt",
                table: "ReportDetails",
                columns: new[] { "OrgId", "ReportId", "ColumnKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_ReportId",
                schema: "rpt",
                table: "ReportDetails",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_Default",
                schema: "rpt",
                table: "ReportViews",
                columns: new[] { "OrgId", "ReportId", "OwnerUserId" },
                unique: true,
                filter: "\"IsDefault\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_Name",
                schema: "rpt",
                table: "ReportViews",
                columns: new[] { "OrgId", "ReportId", "OwnerUserId", "ViewName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_OrgId",
                schema: "rpt",
                table: "ReportViews",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_ReportId",
                schema: "rpt",
                table: "ReportViews",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Order",
                schema: "rpt",
                table: "Reports",
                columns: new[] { "OrgId", "Module", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrgId",
                schema: "rpt",
                table: "Reports",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrgId_ReportKey",
                schema: "rpt",
                table: "Reports",
                columns: new[] { "OrgId", "ReportKey" },
                unique: true);

            // Row-level security on every rpt table. The EF query filter is the
            // first line of defence; this is the one that holds if a query ever
            // runs without it. Written by hand because EF does not model policies.
            foreach (string table in new[] { "Reports", "ReportDetails", "ReportViews" })
            {
                migrationBuilder.Sql($"ALTER TABLE rpt.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON rpt.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "Reports", "ReportDetails", "ReportViews" })
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON rpt.\"{table}\";");
            }

            migrationBuilder.DropTable(
                name: "ReportDetails",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "ReportViews",
                schema: "rpt");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "rpt");
        }
    }
}
