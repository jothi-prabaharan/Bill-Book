using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentLineConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOneTime",
                schema: "plt",
                table: "Configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                column: "IsOneTime",
                value: false);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000002"),
                column: "IsOneTime",
                value: false);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000003"),
                column: "IsOneTime",
                value: false);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000004"),
                column: "IsOneTime",
                value: false);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"),
                column: "IsOneTime",
                value: true);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"),
                column: "IsOneTime",
                value: true);

            migrationBuilder.UpdateData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"),
                column: "IsOneTime",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOneTime",
                schema: "plt",
                table: "Configurations");
        }
    }
}
