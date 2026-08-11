using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Platform.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentLineConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "plt",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000005"), "Documents", "documents.allowFreeTextLines", null, null, "Boolean", "Let a sales or purchase line carry a description, quantity and price with no item behind it. Such a line moves no stock and posts to a named account, so it never appears in a sales-by-item report. Turn it off to require every line to name an item.", true, null, null, "Allow Lines Without An Item", null, "true" },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), "Documents", "documents.discountLevel", null, null, "Text", "Line, Header, or Both. A header discount is apportioned across the lines by taxable value before tax is computed, because GST is charged per line and a discount that never reaches a line cannot reduce it.", true, null, null, "Discount Entered At", null, "Line" },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), "Documents", "documents.discountBeforeTax", null, null, "Boolean", "On by default: the discount comes off before GST is computed, so it reduces the tax. Turn it off for a discount applied after tax — the tax is then charged on the full value and the discount only reduces what is collected. That is a real settlement or cash discount, but it does not reduce GST liability, and it must still be shown on the invoice.", true, null, null, "Discount Reduces Taxable Value", null, "true" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
