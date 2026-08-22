using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <summary>
    /// Why an order was closed with less delivered than ordered.
    ///
    /// <c>FulfilmentStatus.Closed</c> covers two different facts — everything
    /// went out, or nothing further is coming — and the delivered quantities
    /// cannot tell them apart, which is the reason that status is a column
    /// rather than arithmetic in the first place. This is the other half of it:
    /// null means the order closed because it was fulfilled, set means somebody
    /// decided to stop and this records what was agreed.
    ///
    /// Nullable and additive; nothing existing changes.
    /// </summary>
    public partial class AddShortCloseReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShortCloseReason",
                schema: "sal",
                table: "SalesOrders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortCloseReason",
                schema: "sal",
                table: "SalesOrders");
        }
    }
}
