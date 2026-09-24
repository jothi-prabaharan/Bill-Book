using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class FixedAssetBillLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PurchaseBillDetailId",
                schema: "acc",
                table: "FixedAssets",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_OrgId_PurchaseBillDetailId",
                schema: "acc",
                table: "FixedAssets",
                columns: new[] { "OrgId", "PurchaseBillDetailId" },
                unique: true,
                filter: "\"PurchaseBillDetailId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FixedAssets_OrgId_PurchaseBillDetailId",
                schema: "acc",
                table: "FixedAssets");

            migrationBuilder.DropColumn(
                name: "PurchaseBillDetailId",
                schema: "acc",
                table: "FixedAssets");
        }
    }
}
