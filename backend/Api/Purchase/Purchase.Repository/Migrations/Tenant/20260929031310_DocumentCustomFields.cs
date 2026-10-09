using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class DocumentCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "pur",
                table: "PurchaseOrders",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "pur",
                table: "GoodsReceipts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "pur",
                table: "DebitNotes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "pur",
                table: "Bills",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "pur",
                table: "Bills");
        }
    }
}
