using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_OrgId",
                schema: "rpt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_ReportViews_OrgId",
                schema: "rpt",
                table: "ReportViews");

            migrationBuilder.DropIndex(
                name: "IX_ReportDetails_OrgId",
                schema: "rpt",
                table: "ReportDetails");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "rpt",
                table: "Reports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "rpt",
                table: "ReportViews",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "rpt",
                table: "ReportDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CustomerId_OrgId",
                schema: "rpt",
                table: "Reports",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_CustomerId_OrgId",
                schema: "rpt",
                table: "ReportViews",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_CustomerId_OrgId",
                schema: "rpt",
                table: "ReportDetails",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_CustomerId_OrgId",
                schema: "rpt",
                table: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_ReportViews_CustomerId_OrgId",
                schema: "rpt",
                table: "ReportViews");

            migrationBuilder.DropIndex(
                name: "IX_ReportDetails_CustomerId_OrgId",
                schema: "rpt",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "rpt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "rpt",
                table: "ReportViews");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "rpt",
                table: "ReportDetails");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_OrgId",
                schema: "rpt",
                table: "Reports",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportViews_OrgId",
                schema: "rpt",
                table: "ReportViews",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportDetails_OrgId",
                schema: "rpt",
                table: "ReportDetails",
                column: "OrgId");
        }
    }
}
