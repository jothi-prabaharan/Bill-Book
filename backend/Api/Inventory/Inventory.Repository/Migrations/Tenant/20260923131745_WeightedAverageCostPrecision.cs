using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class WeightedAverageCostPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "inv",
                table: "StockMovements",
                type: "numeric(28,12)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ResultingWeightedAverageCost",
                schema: "inv",
                table: "StockMovements",
                type: "numeric(28,12)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightedAverageCost",
                schema: "inv",
                table: "ItemStock",
                type: "numeric(28,12)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnitCost",
                schema: "inv",
                table: "StockMovements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "ResultingWeightedAverageCost",
                schema: "inv",
                table: "StockMovements",
                type: "numeric(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "WeightedAverageCost",
                schema: "inv",
                table: "ItemStock",
                type: "numeric(18,6)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)");
        }
    }
}
