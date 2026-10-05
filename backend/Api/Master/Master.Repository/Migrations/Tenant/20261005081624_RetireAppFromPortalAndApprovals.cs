using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class RetireAppFromPortalAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The grant's app becomes the branch's business type (TK-136). Only
            // School (App 2) opened a different portal, so only it carries over;
            // every other link opens the statement portal, as a null does.
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "con",
                table: "PortalGrants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PortalGrants",
                keyColumns: ["App"],
                keyColumnTypes: ["integer"],
                keyValues: [2],
                columns: ["BusinessType"],
                columnTypes: ["character varying(20)"],
                values: ["School"],
                schema: "con");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "con",
                table: "PortalGrants");

            // Derived from the request kind and read by nothing.
            migrationBuilder.DropColumn(
                name: "App",
                schema: "apr",
                table: "ApprovalWorkflows");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "con",
                table: "PortalGrants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.UpdateData(
                table: "PortalGrants",
                keyColumns: ["BusinessType"],
                keyColumnTypes: ["character varying(20)"],
                keyValues: ["School"],
                columns: ["App"],
                columnTypes: ["integer"],
                values: [2],
                schema: "con");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                schema: "con",
                table: "PortalGrants");

            migrationBuilder.AddColumn<int>(
                name: "App",
                schema: "apr",
                table: "ApprovalWorkflows",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }
    }
}
