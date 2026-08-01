using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <summary>
    /// <c>BranchId</c> goes: an <c>OrgId</c> now <i>is</i> a branch, so a second
    /// column naming one on the same row said the same thing twice — and only
    /// <c>OrgId</c> was ever enforced.
    /// </summary>
    public partial class DropBranchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BranchId", schema: "inv", table: "StockMovements");
            migrationBuilder.DropColumn(name: "BranchId", schema: "inv", table: "Warehouses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BranchId", schema: "inv", table: "StockMovements",
                type: "bigint", nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BranchId", schema: "inv", table: "Warehouses",
                type: "bigint", nullable: true);
        }
    }
}
