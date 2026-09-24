using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class EmployeeAndHrmModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[] { 10, 14, false, "people", null, null, 10, "users", true, false, null, null, "employee", "People", null, null, null, "Rail" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "Apps", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[,]
                {
                    { 121, 14, "employee.view", null, null, null, null, null, "employee" },
                    { 122, 14, "employee.create", null, null, null, null, null, "employee" },
                    { 123, 14, "employee.edit", null, null, null, null, null, "employee" },
                    { 124, 14, "employee.approve", null, null, null, null, null, "employee" },
                    { 125, 14, "employee.void", null, null, null, null, null, "employee" },
                    { 126, 14, "employee.delete", null, null, null, null, null, "employee" },
                    { 127, 14, "employee.print", null, null, null, null, null, "employee" },
                    { 128, 14, "employee.export", null, null, null, null, null, "employee" },
                    { 129, 14, "employee.import", null, null, null, null, null, "employee" },
                    { 130, 14, "employee.AllUserData", null, null, null, null, null, "employee" },
                    { 131, 4, "hrm.view", null, null, null, null, null, "hrm" },
                    { 132, 4, "hrm.create", null, null, null, null, null, "hrm" },
                    { 133, 4, "hrm.edit", null, null, null, null, null, "hrm" },
                    { 134, 4, "hrm.approve", null, null, null, null, null, "hrm" },
                    { 135, 4, "hrm.void", null, null, null, null, null, "hrm" },
                    { 136, 4, "hrm.delete", null, null, null, null, null, "hrm" },
                    { 137, 4, "hrm.print", null, null, null, null, null, "hrm" },
                    { 138, 4, "hrm.export", null, null, null, null, null, "hrm" },
                    { 139, 4, "hrm.import", null, null, null, null, null, "hrm" },
                    { 140, 4, "hrm.AllUserData", null, null, null, null, null, "hrm" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 2000000121L, null, null, null, null, 121, 1000002 },
                    { 2000000122L, null, null, null, null, 122, 1000002 },
                    { 2000000123L, null, null, null, null, 123, 1000002 },
                    { 2000000124L, null, null, null, null, 124, 1000002 },
                    { 2000000125L, null, null, null, null, 125, 1000002 },
                    { 2000000126L, null, null, null, null, 126, 1000002 },
                    { 2000000127L, null, null, null, null, 127, 1000002 },
                    { 2000000128L, null, null, null, null, 128, 1000002 },
                    { 2000000129L, null, null, null, null, 129, 1000002 },
                    { 2000000130L, null, null, null, null, 130, 1000002 },
                    { 4000000121L, null, null, null, null, 121, 1000004 },
                    { 4000000122L, null, null, null, null, 122, 1000004 },
                    { 4000000123L, null, null, null, null, 123, 1000004 },
                    { 4000000124L, null, null, null, null, 124, 1000004 },
                    { 4000000125L, null, null, null, null, 125, 1000004 },
                    { 4000000126L, null, null, null, null, 126, 1000004 },
                    { 4000000127L, null, null, null, null, 127, 1000004 },
                    { 4000000128L, null, null, null, null, 128, 1000004 },
                    { 4000000129L, null, null, null, null, 129, 1000004 },
                    { 4000000130L, null, null, null, null, 130, 1000004 },
                    { 4000000131L, null, null, null, null, 131, 1000004 },
                    { 4000000132L, null, null, null, null, 132, 1000004 },
                    { 4000000133L, null, null, null, null, 133, 1000004 },
                    { 4000000134L, null, null, null, null, 134, 1000004 },
                    { 4000000135L, null, null, null, null, 135, 1000004 },
                    { 4000000136L, null, null, null, null, 136, 1000004 },
                    { 4000000137L, null, null, null, null, 137, 1000004 },
                    { 4000000138L, null, null, null, null, 138, 1000004 },
                    { 4000000139L, null, null, null, null, 139, 1000004 },
                    { 4000000140L, null, null, null, null, 140, 1000004 },
                    { 8000000121L, null, null, null, null, 121, 1000008 },
                    { 8000000122L, null, null, null, null, 122, 1000008 },
                    { 8000000123L, null, null, null, null, 123, 1000008 },
                    { 8000000124L, null, null, null, null, 124, 1000008 },
                    { 8000000125L, null, null, null, null, 125, 1000008 },
                    { 8000000126L, null, null, null, null, 126, 1000008 },
                    { 8000000127L, null, null, null, null, 127, 1000008 },
                    { 8000000128L, null, null, null, null, 128, 1000008 },
                    { 8000000129L, null, null, null, null, 129, 1000008 },
                    { 8000000130L, null, null, null, null, 130, 1000008 }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 118, 14, false, "people-g1", null, null, 1, null, true, false, null, null, null, null, 10, null, null, "Group" },
                    { 1107, 14, true, "emp", null, null, 1, "id-card", true, true, null, null, "employee", "Employees", 118, "/hrm/employees", "Employee", "Item" },
                    { 1108, 14, false, "hrorg", null, null, 2, "network", true, false, null, null, "employee", "Organisation setup", 118, "/hrm/organisation", null, "Item" },
                    { 1109, 4, true, "ann", null, null, 3, "megaphone", true, false, null, null, "hrm", "Announcements", 118, "/hrm/announcements", "Announcement", "Item" },
                    { 1110, 4, true, "pol", null, null, 4, "file-text", true, false, null, null, "hrm", "Policies", 118, "/hrm/policies", "Policy", "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 383, "view", null, null, 1107, null, null, "employee", "employee.view" },
                    { 384, "create", null, null, 1107, null, null, "employee", "employee.create" },
                    { 385, "edit", null, null, 1107, null, null, "employee", "employee.edit" },
                    { 386, "export", null, null, 1107, null, null, "employee", "employee.export" },
                    { 387, "view", null, null, 1108, null, null, "employee", "employee.view" },
                    { 388, "edit", null, null, 1108, null, null, "employee", "employee.edit" },
                    { 389, "view", null, null, 1109, null, null, "hrm", "hrm.view" },
                    { 390, "create", null, null, 1109, null, null, "hrm", "hrm.create" },
                    { 391, "edit", null, null, 1109, null, null, "hrm", "hrm.edit" },
                    { 392, "view", null, null, 1110, null, null, "hrm", "hrm.view" },
                    { 393, "create", null, null, 1110, null, null, "hrm", "hrm.create" },
                    { 394, "edit", null, null, 1110, null, null, "hrm", "hrm.edit" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 383);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 384);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 385);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 386);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 387);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 388);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 389);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 390);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 391);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 392);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 393);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 394);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 121);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 122);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 123);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 124);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 125);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 126);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 127);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 128);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 129);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 130);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 131);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 132);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 133);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 134);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 135);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 136);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 137);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 138);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 139);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 140);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000131L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000132L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000133L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000134L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000135L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000136L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000137L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000138L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000139L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000140L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000121L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000122L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000123L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000124L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000125L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000126L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000127L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000128L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000129L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000130L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1107);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1108);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1109);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1110);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 118);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 10);
        }
    }
}
