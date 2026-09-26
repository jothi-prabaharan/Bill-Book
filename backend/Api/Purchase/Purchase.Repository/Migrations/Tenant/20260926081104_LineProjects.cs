using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class LineProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "pur",
                table: "PurchaseOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "pur",
                table: "DebitNoteDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "pur",
                table: "BillDetails",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "pur",
                table: "PurchaseOrderDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "pur",
                table: "GoodsReceiptDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "pur",
                table: "DebitNoteDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "pur",
                table: "BillDetails");
        }
    }
}
