using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class RecurringBillingMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 1142, 256, null, true, "rin", null, null, 6, "repeat", true, true, null, null, "sales", "Recurring invoices", 104, "/sales/recurring-invoices", "Recurring invoice", "Item" },
                    { 1143, 256, null, true, "rbl", null, null, 5, "repeat", true, true, null, null, "purchase", "Recurring bills", 103, "/purchase/recurring-bills", "Recurring bill", "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 485, "view", null, null, 1142, null, null, "sales", "sales.view" },
                    { 486, "create", null, null, 1142, null, null, "sales", "sales.create" },
                    { 487, "edit", null, null, 1142, null, null, "sales", "sales.edit" },
                    { 488, "view", null, null, 1143, null, null, "purchase", "purchase.view" },
                    { 489, "create", null, null, 1143, null, null, "purchase", "purchase.create" },
                    { 490, "edit", null, null, 1143, null, null, "purchase", "purchase.edit" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 485);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 486);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 487);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 488);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 489);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 490);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1142);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1143);
        }
    }
}
