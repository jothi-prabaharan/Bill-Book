using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddStockLedgerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LedgerAttempts",
                schema: "inv",
                table: "StockMovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LedgerError",
                schema: "inv",
                table: "StockMovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LedgerPostedAt",
                schema: "inv",
                table: "StockMovements",
                type: "timestamp with time zone",
                nullable: true);

            // 'Pending', not the empty string EF generates for a string-mapped
            // enum. Two reasons, and the first is fatal on its own: an empty
            // string is not one of the enum's names, so every movement recorded
            // before this migration would fail to materialize.
            //
            // The second is what the value should be. Movements that already
            // exist have never been posted, and the ledger is empty — so
            // queueing them is what makes stock and the general ledger agree,
            // which is the entire point of the feature. Each posts at its own
            // movement date, so the history lands where it belongs rather than
            // all of it landing today.
            migrationBuilder.AddColumn<string>(
                name: "LedgerStatus",
                schema: "inv",
                table: "StockMovements",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LedgerQueue",
                schema: "inv",
                table: "StockMovements",
                columns: new[] { "OrgId", "MovementDate", "StockMovementId" },
                filter: "\"LedgerStatus\" IN ('Pending', 'InProgress')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockMovements_LedgerQueue",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LedgerAttempts",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LedgerError",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LedgerPostedAt",
                schema: "inv",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LedgerStatus",
                schema: "inv",
                table: "StockMovements");
        }
    }
}
