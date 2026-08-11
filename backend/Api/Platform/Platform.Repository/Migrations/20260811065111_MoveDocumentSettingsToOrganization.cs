using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Platform.Repository.Migrations
{
    /// <inheritdoc />
    public partial class MoveDocumentSettingsToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "plt",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000007"));

            migrationBuilder.DropColumn(
                name: "IsOneTime",
                schema: "plt",
                table: "Configurations");

            migrationBuilder.AddColumn<bool>(
                name: "AllowFreeTextLines",
                schema: "plt",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiscountBeforeTax",
                schema: "plt",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountLevel",
                schema: "plt",
                table: "Organizations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowFreeTextLines",
                schema: "plt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DiscountBeforeTax",
                schema: "plt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "DiscountLevel",
                schema: "plt",
                table: "Organizations");

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

            migrationBuilder.InsertData(
                schema: "plt",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsOneTime", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000005"), "Documents", "documents.allowFreeTextLines", null, null, "Boolean", "Let a sales or purchase line carry a description, quantity and price with no item behind it. Such a line moves no stock and posts to a named account, so it never appears in a sales-by-item report. Turn it off to require every line to name an item.", true, true, null, null, "Allow Lines Without An Item", null, "true" },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), "Documents", "documents.discountLevel", null, null, "Text", "Line, Header, or Both. A header discount is apportioned across the lines by taxable value before tax is computed, because GST is charged per line and a discount that never reaches a line cannot reduce it.", true, true, null, null, "Discount Entered At", null, "Line" },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), "Documents", "documents.discountBeforeTax", null, null, "Boolean", "On by default: the discount comes off before GST is computed, so it reduces the tax. Turn it off for a discount applied after tax — the tax is then charged on the full value and the discount only reduces what is collected. That is a real settlement or cash discount, but it does not reduce GST liability, and it must still be shown on the invoice.", true, true, null, null, "Discount Reduces Taxable Value", null, "true" }
                });
        }
    }
}
