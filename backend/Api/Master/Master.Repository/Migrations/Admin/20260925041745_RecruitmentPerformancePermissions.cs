using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class RecruitmentPerformancePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "Apps", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[,]
                {
                    { 251, 4, "recruitment.view", null, null, null, null, null, "recruitment" },
                    { 252, 4, "recruitment.create", null, null, null, null, null, "recruitment" },
                    { 253, 4, "recruitment.edit", null, null, null, null, null, "recruitment" },
                    { 254, 4, "recruitment.approve", null, null, null, null, null, "recruitment" },
                    { 255, 4, "recruitment.void", null, null, null, null, null, "recruitment" },
                    { 256, 4, "recruitment.delete", null, null, null, null, null, "recruitment" },
                    { 257, 4, "recruitment.print", null, null, null, null, null, "recruitment" },
                    { 258, 4, "recruitment.export", null, null, null, null, null, "recruitment" },
                    { 259, 4, "recruitment.import", null, null, null, null, null, "recruitment" },
                    { 260, 4, "recruitment.AllUserData", null, null, null, null, null, "recruitment" },
                    { 261, 4, "performance.view", null, null, null, null, null, "performance" },
                    { 262, 4, "performance.create", null, null, null, null, null, "performance" },
                    { 263, 4, "performance.edit", null, null, null, null, null, "performance" },
                    { 264, 4, "performance.approve", null, null, null, null, null, "performance" },
                    { 265, 4, "performance.void", null, null, null, null, null, "performance" },
                    { 266, 4, "performance.delete", null, null, null, null, null, "performance" },
                    { 267, 4, "performance.print", null, null, null, null, null, "performance" },
                    { 268, 4, "performance.export", null, null, null, null, null, "performance" },
                    { 269, 4, "performance.import", null, null, null, null, null, "performance" },
                    { 270, 4, "performance.AllUserData", null, null, null, null, null, "performance" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 4000000251L, null, null, null, null, 251, 1000004 },
                    { 4000000252L, null, null, null, null, 252, 1000004 },
                    { 4000000253L, null, null, null, null, 253, 1000004 },
                    { 4000000254L, null, null, null, null, 254, 1000004 },
                    { 4000000255L, null, null, null, null, 255, 1000004 },
                    { 4000000256L, null, null, null, null, 256, 1000004 },
                    { 4000000257L, null, null, null, null, 257, 1000004 },
                    { 4000000258L, null, null, null, null, 258, 1000004 },
                    { 4000000259L, null, null, null, null, 259, 1000004 },
                    { 4000000260L, null, null, null, null, 260, 1000004 },
                    { 4000000261L, null, null, null, null, 261, 1000004 },
                    { 4000000262L, null, null, null, null, 262, 1000004 },
                    { 4000000263L, null, null, null, null, 263, 1000004 },
                    { 4000000264L, null, null, null, null, 264, 1000004 },
                    { 4000000265L, null, null, null, null, 265, 1000004 },
                    { 4000000266L, null, null, null, null, 266, 1000004 },
                    { 4000000267L, null, null, null, null, 267, 1000004 },
                    { 4000000268L, null, null, null, null, 268, 1000004 },
                    { 4000000269L, null, null, null, null, 269, 1000004 },
                    { 4000000270L, null, null, null, null, 270, 1000004 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 251);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 252);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 253);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 254);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 255);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 256);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 257);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 258);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 259);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 260);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 261);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 262);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 263);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 264);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 265);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 266);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 267);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 268);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 269);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 270);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000251L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000252L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000253L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000254L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000255L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000256L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000257L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000258L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000259L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000260L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000261L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000262L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000263L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000264L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000265L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000266L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000267L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000268L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000269L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000270L);
        }
    }
}
