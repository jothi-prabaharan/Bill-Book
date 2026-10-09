using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddWarehouseSiteId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SiteId",
                schema: "inv",
                table: "Warehouses",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_SiteId",
                schema: "inv",
                table: "Warehouses",
                columns: new[] { "OrgId", "SiteId" },
                filter: "\"SiteId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_SiteId",
                schema: "inv",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "SiteId",
                schema: "inv",
                table: "Warehouses");
        }
    }
}
