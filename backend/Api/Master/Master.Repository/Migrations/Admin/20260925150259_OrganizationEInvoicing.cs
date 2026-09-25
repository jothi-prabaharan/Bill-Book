using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class OrganizationEInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EInvoiceFrom",
                schema: "mst",
                table: "Organizations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EwayBillEnabled",
                schema: "mst",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EInvoiceFrom",
                schema: "mst",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EwayBillEnabled",
                schema: "mst",
                table: "Organizations");
        }
    }
}
