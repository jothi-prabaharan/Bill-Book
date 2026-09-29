using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DocumentCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "sal",
                table: "SalesOrders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "sal",
                table: "Quotes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "sal",
                table: "Invoices",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "sal",
                table: "DeliveryChallans",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "sal",
                table: "CreditNotes",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "sal",
                table: "CreditNotes");
        }
    }
}
