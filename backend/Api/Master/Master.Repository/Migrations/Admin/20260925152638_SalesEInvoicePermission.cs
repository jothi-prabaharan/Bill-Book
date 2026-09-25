using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class SalesEInvoicePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 442, "einvoice", null, null, 1019, null, null, "sales", "sales.einvoice" },
                    { 443, "einvoice", null, null, 1020, null, null, "sales", "sales.einvoice" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "Apps", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[] { 10003, 1, "sales.einvoice", null, null, null, null, null, "sales" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 900110003L, null, null, null, null, 10003, 1 },
                    { 900210003L, null, null, null, null, 10003, 2 },
                    { 900410003L, null, null, null, null, 10003, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 442);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 443);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 10003);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 900110003L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 900210003L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 900410003L);
        }
    }
}
