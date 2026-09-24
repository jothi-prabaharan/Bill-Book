using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AppOwnerRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 2000000091L, null, null, null, null, 91, 1000002 },
                    { 2000000092L, null, null, null, null, 92, 1000002 },
                    { 2000000093L, null, null, null, null, 93, 1000002 },
                    { 2000000094L, null, null, null, null, 94, 1000002 },
                    { 2000000095L, null, null, null, null, 95, 1000002 },
                    { 2000000096L, null, null, null, null, 96, 1000002 },
                    { 2000000097L, null, null, null, null, 97, 1000002 },
                    { 2000000098L, null, null, null, null, 98, 1000002 },
                    { 2000000099L, null, null, null, null, 99, 1000002 },
                    { 2000000100L, null, null, null, null, 100, 1000002 },
                    { 4000000091L, null, null, null, null, 91, 1000004 },
                    { 4000000092L, null, null, null, null, 92, 1000004 },
                    { 4000000093L, null, null, null, null, 93, 1000004 },
                    { 4000000094L, null, null, null, null, 94, 1000004 },
                    { 4000000095L, null, null, null, null, 95, 1000004 },
                    { 4000000096L, null, null, null, null, 96, 1000004 },
                    { 4000000097L, null, null, null, null, 97, 1000004 },
                    { 4000000098L, null, null, null, null, 98, 1000004 },
                    { 4000000099L, null, null, null, null, 99, 1000004 },
                    { 4000000100L, null, null, null, null, 100, 1000004 },
                    { 8000000091L, null, null, null, null, 91, 1000008 },
                    { 8000000092L, null, null, null, null, 92, 1000008 },
                    { 8000000093L, null, null, null, null, 93, 1000008 },
                    { 8000000094L, null, null, null, null, 94, 1000008 },
                    { 8000000095L, null, null, null, null, 95, 1000008 },
                    { 8000000096L, null, null, null, null, 96, 1000008 },
                    { 8000000097L, null, null, null, null, 97, 1000008 },
                    { 8000000098L, null, null, null, null, 98, 1000008 },
                    { 8000000099L, null, null, null, null, 99, 1000008 },
                    { 8000000100L, null, null, null, null, 100, 1000008 }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Roles",
                columns: new[] { "RoleId", "App", "CreatedAt", "CreatedBy", "CustomerId", "Description", "DisplayName", "IsActive", "IsSystemRole", "ModifiedAt", "ModifiedBy", "SystemName" },
                values: new object[,]
                {
                    { 1000002, 2, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 1000004, 4, null, null, null, null, "Owner", true, true, null, null, "Owner" },
                    { 1000008, 8, null, null, null, null, "Owner", true, true, null, null, "Owner" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 2000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000091L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000092L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000093L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000094L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000095L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000096L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000097L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000098L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000099L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 8000000100L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000002);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000004);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Roles",
                keyColumn: "RoleId",
                keyValue: 1000008);
        }
    }
}
