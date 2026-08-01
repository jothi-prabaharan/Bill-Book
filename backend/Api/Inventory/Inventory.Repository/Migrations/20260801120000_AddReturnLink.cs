using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// A return names the issue it reverses, so the stock can go back onto the
    /// layers it left from at the cost it left at. Without the link a return
    /// re-enters at today's running average, and buy-sell-return changes what
    /// the stock is worth even though nothing was bought or sold on net.
    /// </summary>
    public partial class AddReturnLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReturnsStockMovementId",
                schema: "inv",
                table: "StockMovements",
                type: "bigint",
                nullable: true);

            // Finding what has already come back against an issue, so two
            // partial returns cannot together give back more than went out.
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Returns",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "ReturnsStockMovementId" },
                filter: "\"ReturnsStockMovementId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_Returns", schema: "inv", table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ReturnsStockMovementId", schema: "inv", table: "StockMovements");
        }
    }
}
