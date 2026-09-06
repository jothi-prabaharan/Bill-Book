using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "pur",
                table: "PurchaseOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "pur",
                table: "GoodsReceipts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "pur",
                table: "DebitNotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "pur",
                table: "Bills",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "pur",
                table: "Bills");
        }
    }
}
