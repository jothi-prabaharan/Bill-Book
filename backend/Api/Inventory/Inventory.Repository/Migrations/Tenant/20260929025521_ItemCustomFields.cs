using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ItemCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "inv",
                table: "Items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_CustomFields",
                schema: "inv",
                table: "Items",
                column: "CustomFields")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_CustomFields",
                schema: "inv",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "inv",
                table: "Items");
        }
    }
}
