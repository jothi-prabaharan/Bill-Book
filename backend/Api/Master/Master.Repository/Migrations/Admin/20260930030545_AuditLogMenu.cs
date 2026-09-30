using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AuditLogMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[] { 1139, 15, false, "audit", null, null, 7, "history", true, true, null, null, "settings", "Audit log", 115, "/settings/audit-log", null, "Item" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[] { 478, "view", null, null, 1139, null, null, "settings", "settings.view" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 478);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1139);
        }
    }
}
