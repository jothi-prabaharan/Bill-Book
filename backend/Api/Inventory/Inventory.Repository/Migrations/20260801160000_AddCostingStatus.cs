using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// Costing becomes asynchronous, so a movement has to say where it has got
    /// to. Without this a movement with no cost is indistinguishable from one
    /// that genuinely cost nothing.
    ///
    /// These columns are also the queue: the worker reads Pending rows in
    /// (item, date, id) order and claims each with a guarded status change, which
    /// is what gives ordering and exactly-once without a broker in the path.
    /// </summary>
    public partial class AddCostingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostingStatus",
                schema: "inv",
                table: "StockMovements",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                defaultValue: "Costed");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CostedAt",
                schema: "inv",
                table: "StockMovements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostingAttempts",
                schema: "inv",
                table: "StockMovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CostingError",
                schema: "inv",
                table: "StockMovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ItemBatchId",
                schema: "inv",
                table: "StockMovements",
                type: "bigint",
                nullable: true);

            // The queue read, in the order the worker consumes it. Filtered,
            // because settled movements are the overwhelming majority and the
            // worker never looks at them.
            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CostingQueue",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "ItemId", "MovementDate", "StockMovementId" },
                filter: "\"CostingStatus\" IN ('Pending', 'InProgress')");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ItemBatchId",
                schema: "inv",
                table: "StockMovements",
                column: "ItemBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ItemBatches_ItemBatchId",
                schema: "inv",
                table: "StockMovements",
                column: "ItemBatchId",
                principalSchema: "inv",
                principalTable: "ItemBatches",
                principalColumn: "ItemBatchId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ItemBatches_ItemBatchId",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ItemBatchId", schema: "inv", table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_CostingQueue", schema: "inv", table: "StockMovements");

            foreach (string column in new[]
                { "ItemBatchId", "CostingError", "CostingAttempts", "CostedAt", "CostingStatus" })
            {
                migrationBuilder.DropColumn(name: column, schema: "inv", table: "StockMovements");
            }
        }
    }
}
