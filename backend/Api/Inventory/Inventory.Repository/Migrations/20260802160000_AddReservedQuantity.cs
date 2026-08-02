using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// Stock promised to a confirmed order but not yet issued.
    ///
    /// <b>Never subtracted from QuantityOnHand.</b> The stock is still on the
    /// shelf and still worth what it cost, so anything valuing stock keeps
    /// reading on-hand; only "can I sell this" changes, and that reads
    /// <c>QuantityOnHand - QuantityReserved</c>. Storing availability as a third
    /// column would be a number that can disagree with the two it comes from.
    ///
    /// The check constraint is what stops the pair going incoherent: a release
    /// that ran twice would drive the reserve negative and quietly free stock
    /// nobody released, and a reservation larger than the stock held would
    /// promise what is not there.
    ///
    /// Existing rows default to zero, which is a fact rather than a
    /// convenience — nothing has ever reserved anything.
    /// </summary>
    public partial class AddReservedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuantityReserved",
                schema: "inv",
                table: "ItemStock",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "chk_stock_reserved",
                schema: "inv",
                table: "ItemStock",
                sql: "\"QuantityReserved\" >= 0 AND \"QuantityReserved\" <= \"QuantityOnHand\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_stock_reserved",
                schema: "inv",
                table: "ItemStock");

            migrationBuilder.DropColumn(
                name: "QuantityReserved",
                schema: "inv",
                table: "ItemStock");
        }
    }
}
