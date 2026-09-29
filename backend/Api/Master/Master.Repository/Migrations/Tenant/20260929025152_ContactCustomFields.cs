using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ContactCustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "CustomFields",
                schema: "con",
                table: "Contacts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CustomFields",
                schema: "con",
                table: "Contacts",
                column: "CustomFields")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_CustomFields",
                schema: "con",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                schema: "con",
                table: "Contacts");
        }
    }
}
