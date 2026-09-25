using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class InvoiceTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransportDistanceKm",
                schema: "sal",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransportMode",
                schema: "sal",
                table: "Invoices",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransporterId",
                schema: "sal",
                table: "Invoices",
                type: "character varying(15)",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransporterName",
                schema: "sal",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleNo",
                schema: "sal",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransportDistanceKm",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TransportMode",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TransporterId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TransporterName",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VehicleNo",
                schema: "sal",
                table: "Invoices");
        }
    }
}
