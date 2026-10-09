using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class RestaurantSettingsAndMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Configurations",
                columns: new[] { "ConfigId", "Category", "Code", "CreatedAt", "CreatedBy", "DataType", "Description", "IsSystem", "ModifiedAt", "ModifiedBy", "Name", "OrgId", "Value" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000008"), "Restaurant", "restaurant.tables.enabled", null, null, "Boolean", "Floors and tables: seat a dine-in order at a table.", true, null, null, "Tables", null, "false" },
                    { new Guid("a0000000-0000-0000-0000-000000000009"), "Restaurant", "restaurant.kitchen.enabled", null, null, "Boolean", "Send orders to the kitchen as tickets, by station.", true, null, null, "Kitchen (KOT)", null, "false" },
                    { new Guid("a0000000-0000-0000-0000-00000000000a"), "Restaurant", "restaurant.recipes.enabled", null, null, "Boolean", "Deduct each dish's ingredients from stock when its bill posts.", true, null, null, "Recipes", null, "false" },
                    { new Guid("a0000000-0000-0000-0000-00000000000b"), "Restaurant", "restaurant.table.required", null, null, "Text", "When a bill must name a table: Off, DineInOnly or Always. Needs tables.", true, null, null, "Table required", null, "Off" },
                    { new Guid("a0000000-0000-0000-0000-00000000000c"), "Restaurant", "restaurant.kot.required", null, null, "Boolean", "A bill is refused while any of its lines was never sent to the kitchen. Needs the kitchen.", true, null, null, "KOT required", null, "false" },
                    { new Guid("a0000000-0000-0000-0000-00000000000d"), "Restaurant", "restaurant.customer.required", null, null, "Text", "When a bill must name a customer: Off, DineInOnly or Always.", true, null, null, "Customer required", null, "Off" },
                    { new Guid("a0000000-0000-0000-0000-00000000000e"), "Restaurant", "restaurant.waiter.required", null, null, "Boolean", "A bill must name the waiter who served it.", true, null, null, "Waiter required", null, "false" },
                    { new Guid("a0000000-0000-0000-0000-00000000000f"), "Restaurant", "restaurant.serviceCharge.percent", null, null, "Number", "Added to every bill as a line of its own. 0 for none.", true, null, null, "Service charge %", null, "0" },
                    { new Guid("a0000000-0000-0000-0000-000000000010"), "Restaurant", "restaurant.tips.enabled", null, null, "Boolean", "Take a tip on the bill, held for staff rather than earned.", true, null, null, "Tips", null, "false" }
                });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 15,
                column: "BusinessType",
                value: "Restaurant");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 124,
                column: "BusinessType",
                value: "Restaurant");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1140,
                column: "BusinessType",
                value: "Restaurant");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1141,
                columns: new[] { "BusinessType", "DisplayOrder" },
                values: new object[] { "Restaurant", 4 });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 1146, 0, "Restaurant", true, "rst-orders", null, null, 1, "clipboard-list", true, true, null, null, "restaurant", "Orders", 124, "/restaurant/orders", "Order", "Item" },
                    { 1147, 0, "Restaurant", false, "rst-tables", null, null, 2, "layout-grid", true, true, null, null, "restaurant", "Tables", 124, "/restaurant/tables", null, "Item" },
                    { 1148, 0, "Restaurant", false, "rst-kitchen", null, null, 3, "chef-hat", true, true, null, null, "restaurant", "Kitchen display", 124, "/restaurant/kitchen", null, "Item" },
                    { 1149, 0, "Restaurant", true, "rst-recipes", null, null, 5, "notebook-pen", true, true, null, null, "restaurant", "Recipes", 124, "/restaurant/recipes", "Recipe", "Item" },
                    { 1150, 0, "Restaurant", true, "rst-wastage", null, null, 6, "trash-2", true, true, null, null, "restaurant", "Wastage", 124, "/restaurant/wastage", "Wastage entry", "Item" },
                    { 1151, 0, "Restaurant", false, "rst-reports", null, null, 7, "bar-chart-3", true, true, null, null, "restaurant", "Reports", 124, "/restaurant/reports", null, "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 497, "view", null, null, 1146, null, null, "restaurant", "restaurant.view" },
                    { 498, "create", null, null, 1146, null, null, "restaurant", "restaurant.create" },
                    { 499, "edit", null, null, 1146, null, null, "restaurant", "restaurant.edit" },
                    { 500, "void", null, null, 1146, null, null, "restaurant", "restaurant.void" },
                    { 501, "view", null, null, 1147, null, null, "restaurant", "restaurant.view" },
                    { 502, "edit", null, null, 1147, null, null, "restaurant", "restaurant.edit" },
                    { 503, "view", null, null, 1148, null, null, "restaurant", "restaurant.view" },
                    { 504, "edit", null, null, 1148, null, null, "restaurant", "restaurant.edit" },
                    { 505, "view", null, null, 1149, null, null, "restaurant", "restaurant.view" },
                    { 506, "create", null, null, 1149, null, null, "restaurant", "restaurant.create" },
                    { 507, "edit", null, null, 1149, null, null, "restaurant", "restaurant.edit" },
                    { 508, "view", null, null, 1150, null, null, "restaurant", "restaurant.view" },
                    { 509, "create", null, null, 1150, null, null, "restaurant", "restaurant.create" },
                    { 510, "view", null, null, 1151, null, null, "restaurant", "restaurant.view" },
                    { 511, "export", null, null, 1151, null, null, "restaurant", "restaurant.export" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000a"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000b"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000c"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000d"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000e"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-00000000000f"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Configurations",
                keyColumn: "ConfigId",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 497);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 498);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 499);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 500);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 501);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 502);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 503);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 504);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 505);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 506);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 507);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 508);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 509);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 510);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 511);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1146);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1147);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1148);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1149);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1150);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1151);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 15,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 124,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1140,
                column: "BusinessType",
                value: "General");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1141,
                columns: new[] { "BusinessType", "DisplayOrder" },
                values: new object[] { "General", 1 });
        }
    }
}
