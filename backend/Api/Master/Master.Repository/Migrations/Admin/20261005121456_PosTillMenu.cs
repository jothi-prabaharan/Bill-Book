using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class PosTillMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[] { 1153, 8, null, false, "pos", null, null, 6, "monitor", true, false, null, null, "sales", "Point of sale", 104, "/pos", null, "Item" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[] { 516, "approve", null, null, 1153, null, null, "sales", "sales.approve" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 516);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1153);
        }
    }
}
