using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitCostToLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StockMovementId",
                schema: "sal",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                schema: "sal",
                table: "InvoiceDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "StockMovementId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockMovementId",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "StockMovementId",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                schema: "sal",
                table: "DeliveryChallanDetails");
        }
    }
}
