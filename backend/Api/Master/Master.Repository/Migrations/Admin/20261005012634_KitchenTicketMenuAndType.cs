using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class KitchenTicketMenuAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[] { 1152, 0, "Restaurant", false, "pt-kot", null, null, 13, "printer", true, false, null, null, "settings", "Kitchen ticket", 117, "/settings/print-templates/KOT", null, "Item" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "TransactionTypes",
                columns: new[] { "Code", "CreatedAt", "CreatedBy", "IsActive", "IsLedgerPosting", "ModifiedAt", "ModifiedBy", "Name" },
                values: new object[] { "KOT", null, null, true, false, null, null, "Kitchen Ticket" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 512, "view", null, null, 1152, null, null, "settings", "settings.view" },
                    { 513, "create", null, null, 1152, null, null, "settings", "settings.create" },
                    { 514, "edit", null, null, 1152, null, null, "settings", "settings.edit" },
                    { 515, "delete", null, null, 1152, null, null, "settings", "settings.delete" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 512);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 513);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 514);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 515);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "TransactionTypes",
                keyColumn: "Code",
                keyValue: "KOT");

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1152);
        }
    }
}
