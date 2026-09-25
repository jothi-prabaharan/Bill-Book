using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class ClaimsPermissions : Migration
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
                    { 241, 4, "claims.view", null, null, null, null, null, "claims" },
                    { 242, 4, "claims.create", null, null, null, null, null, "claims" },
                    { 243, 4, "claims.edit", null, null, null, null, null, "claims" },
                    { 244, 4, "claims.approve", null, null, null, null, null, "claims" },
                    { 245, 4, "claims.void", null, null, null, null, null, "claims" },
                    { 246, 4, "claims.delete", null, null, null, null, null, "claims" },
                    { 247, 4, "claims.print", null, null, null, null, null, "claims" },
                    { 248, 4, "claims.export", null, null, null, null, null, "claims" },
                    { 249, 4, "claims.import", null, null, null, null, null, "claims" },
                    { 250, 4, "claims.AllUserData", null, null, null, null, null, "claims" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 4000000241L, null, null, null, null, 241, 1000004 },
                    { 4000000242L, null, null, null, null, 242, 1000004 },
                    { 4000000243L, null, null, null, null, 243, 1000004 },
                    { 4000000244L, null, null, null, null, 244, 1000004 },
                    { 4000000245L, null, null, null, null, 245, 1000004 },
                    { 4000000246L, null, null, null, null, 246, 1000004 },
                    { 4000000247L, null, null, null, null, 247, 1000004 },
                    { 4000000248L, null, null, null, null, 248, 1000004 },
                    { 4000000249L, null, null, null, null, 249, 1000004 },
                    { 4000000250L, null, null, null, null, 250, 1000004 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 241);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 242);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 243);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 244);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 245);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 246);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 247);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 248);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 249);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 250);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000241L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000242L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000243L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000244L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000245L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000246L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000247L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000248L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000249L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 4000000250L);
        }
    }
}
