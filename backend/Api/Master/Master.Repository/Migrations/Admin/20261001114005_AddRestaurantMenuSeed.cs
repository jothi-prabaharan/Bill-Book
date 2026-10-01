using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class AddRestaurantMenuSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100272L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100273L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100274L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100275L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100276L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100277L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100278L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100279L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100280L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200272L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200273L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200274L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200275L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200276L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200277L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200278L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200279L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200280L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300272L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300273L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300274L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300275L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300276L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300277L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300278L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300279L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300280L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910400271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910500271L);

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 15, 0, "General", false, "restaurant", null, null, 15, "utensils", true, false, null, null, "restaurant", "Restaurant", null, null, null, "Rail" },
                    { 1140, 0, "General", false, "rst", null, null, 20, "utensils", true, false, null, null, "settings", "Restaurant settings", 114, "/restaurant/settings", null, "Item" }
                });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 271,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.view", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 272,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.create", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 273,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.edit", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 274,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.approve", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 275,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.void", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 276,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.delete", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 277,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.print", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 278,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.export", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 279,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.import", "restaurant" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 280,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "Restaurant", "restaurant.AllUserData", "restaurant" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Permissions",
                columns: new[] { "PermissionId", "AddOns", "BusinessType", "Code", "CreatedAt", "CreatedBy", "Description", "ModifiedAt", "ModifiedBy", "Module" },
                values: new object[,]
                {
                    { 281, 0, "General", "projects.view", null, null, null, null, null, "projects" },
                    { 282, 0, "General", "projects.create", null, null, null, null, null, "projects" },
                    { 283, 0, "General", "projects.edit", null, null, null, null, null, "projects" },
                    { 284, 0, "General", "projects.approve", null, null, null, null, null, "projects" },
                    { 285, 0, "General", "projects.void", null, null, null, null, null, "projects" },
                    { 286, 0, "General", "projects.delete", null, null, null, null, null, "projects" },
                    { 287, 0, "General", "projects.print", null, null, null, null, null, "projects" },
                    { 288, 0, "General", "projects.export", null, null, null, null, null, "projects" },
                    { 289, 0, "General", "projects.import", null, null, null, null, null, "projects" },
                    { 290, 0, "General", "projects.AllUserData", null, null, null, null, null, "projects" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 910100281L, null, null, null, null, 281, 1 },
                    { 910100282L, null, null, null, null, 282, 1 },
                    { 910100283L, null, null, null, null, 283, 1 },
                    { 910100284L, null, null, null, null, 284, 1 },
                    { 910100285L, null, null, null, null, 285, 1 },
                    { 910100286L, null, null, null, null, 286, 1 },
                    { 910100287L, null, null, null, null, 287, 1 },
                    { 910100288L, null, null, null, null, 288, 1 },
                    { 910100289L, null, null, null, null, 289, 1 },
                    { 910100290L, null, null, null, null, 290, 1 },
                    { 910200281L, null, null, null, null, 281, 2 },
                    { 910200282L, null, null, null, null, 282, 2 },
                    { 910200283L, null, null, null, null, 283, 2 },
                    { 910200284L, null, null, null, null, 284, 2 },
                    { 910200285L, null, null, null, null, 285, 2 },
                    { 910200286L, null, null, null, null, 286, 2 },
                    { 910200287L, null, null, null, null, 287, 2 },
                    { 910200288L, null, null, null, null, 288, 2 },
                    { 910200289L, null, null, null, null, 289, 2 },
                    { 910200290L, null, null, null, null, 290, 2 },
                    { 910300281L, null, null, null, null, 281, 3 },
                    { 910300282L, null, null, null, null, 282, 3 },
                    { 910300283L, null, null, null, null, 283, 3 },
                    { 910300284L, null, null, null, null, 284, 3 },
                    { 910300285L, null, null, null, null, 285, 3 },
                    { 910300286L, null, null, null, null, 286, 3 },
                    { 910300287L, null, null, null, null, 287, 3 },
                    { 910300288L, null, null, null, null, 288, 3 },
                    { 910300289L, null, null, null, null, 289, 3 },
                    { 910300290L, null, null, null, null, 290, 3 },
                    { 910400281L, null, null, null, null, 281, 4 },
                    { 910500281L, null, null, null, null, 281, 5 },
                    { 920100271L, null, null, null, null, 271, 1 },
                    { 920100272L, null, null, null, null, 272, 1 },
                    { 920100273L, null, null, null, null, 273, 1 },
                    { 920100274L, null, null, null, null, 274, 1 },
                    { 920100275L, null, null, null, null, 275, 1 },
                    { 920100276L, null, null, null, null, 276, 1 },
                    { 920100277L, null, null, null, null, 277, 1 },
                    { 920100278L, null, null, null, null, 278, 1 },
                    { 920100279L, null, null, null, null, 279, 1 },
                    { 920100280L, null, null, null, null, 280, 1 },
                    { 920200271L, null, null, null, null, 271, 2 },
                    { 920200272L, null, null, null, null, 272, 2 },
                    { 920200273L, null, null, null, null, 273, 2 },
                    { 920200274L, null, null, null, null, 274, 2 },
                    { 920200275L, null, null, null, null, 275, 2 },
                    { 920200276L, null, null, null, null, 276, 2 },
                    { 920200277L, null, null, null, null, 277, 2 },
                    { 920200278L, null, null, null, null, 278, 2 },
                    { 920200279L, null, null, null, null, 279, 2 },
                    { 920200280L, null, null, null, null, 280, 2 }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 479, "view", null, null, 1140, null, null, "settings", "settings.view" },
                    { 480, "edit", null, null, 1140, null, null, "settings", "settings.edit" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "AddOns", "BusinessType", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 124, 0, "General", false, "restaurant-g1", null, null, 1, null, true, false, null, null, null, null, 15, null, null, "Group" },
                    { 1141, 0, "General", true, "pos-menu", null, null, 1, "book-open", true, true, null, null, "restaurant", "Menu", 124, "/restaurant/menu", "Menu Item", "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 481, "view", null, null, 1141, null, null, "restaurant", "restaurant.view" },
                    { 482, "create", null, null, 1141, null, null, "restaurant", "restaurant.create" },
                    { 483, "edit", null, null, 1141, null, null, "restaurant", "restaurant.edit" },
                    { 484, "void", null, null, 1141, null, null, "restaurant", "restaurant.void" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 479);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 480);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 481);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 482);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 483);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 484);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 281);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 282);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 283);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 284);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 285);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 286);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 287);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 288);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 289);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 290);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100281L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100282L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100283L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100284L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100285L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100286L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100287L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100288L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100289L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910100290L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200281L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200282L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200283L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200284L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200285L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200286L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200287L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200288L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200289L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910200290L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300281L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300282L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300283L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300284L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300285L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300286L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300287L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300288L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300289L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910300290L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910400281L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 910500281L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100272L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100273L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100274L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100275L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100276L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100277L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100278L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100279L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920100280L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200271L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200272L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200273L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200274L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200275L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200276L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200277L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200278L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200279L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "RolePermissions",
                keyColumn: "RolePermissionId",
                keyValue: 920200280L);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1140);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1141);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 124);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 15);

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 271,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.view", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 272,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.create", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 273,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.edit", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 274,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.approve", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 275,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.void", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 276,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.delete", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 277,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.print", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 278,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.export", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 279,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.import", "projects" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Permissions",
                keyColumn: "PermissionId",
                keyValue: 280,
                columns: new[] { "BusinessType", "Code", "Module" },
                values: new object[] { "General", "projects.AllUserData", "projects" });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "RolePermissions",
                columns: new[] { "RolePermissionId", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 910100271L, null, null, null, null, 271, 1 },
                    { 910100272L, null, null, null, null, 272, 1 },
                    { 910100273L, null, null, null, null, 273, 1 },
                    { 910100274L, null, null, null, null, 274, 1 },
                    { 910100275L, null, null, null, null, 275, 1 },
                    { 910100276L, null, null, null, null, 276, 1 },
                    { 910100277L, null, null, null, null, 277, 1 },
                    { 910100278L, null, null, null, null, 278, 1 },
                    { 910100279L, null, null, null, null, 279, 1 },
                    { 910100280L, null, null, null, null, 280, 1 },
                    { 910200271L, null, null, null, null, 271, 2 },
                    { 910200272L, null, null, null, null, 272, 2 },
                    { 910200273L, null, null, null, null, 273, 2 },
                    { 910200274L, null, null, null, null, 274, 2 },
                    { 910200275L, null, null, null, null, 275, 2 },
                    { 910200276L, null, null, null, null, 276, 2 },
                    { 910200277L, null, null, null, null, 277, 2 },
                    { 910200278L, null, null, null, null, 278, 2 },
                    { 910200279L, null, null, null, null, 279, 2 },
                    { 910200280L, null, null, null, null, 280, 2 },
                    { 910300271L, null, null, null, null, 271, 3 },
                    { 910300272L, null, null, null, null, 272, 3 },
                    { 910300273L, null, null, null, null, 273, 3 },
                    { 910300274L, null, null, null, null, 274, 3 },
                    { 910300275L, null, null, null, null, 275, 3 },
                    { 910300276L, null, null, null, null, 276, 3 },
                    { 910300277L, null, null, null, null, 277, 3 },
                    { 910300278L, null, null, null, null, 278, 3 },
                    { 910300279L, null, null, null, null, 279, 3 },
                    { 910300280L, null, null, null, null, 280, 3 },
                    { 910400271L, null, null, null, null, 271, 4 },
                    { 910500271L, null, null, null, null, 271, 5 }
                });
        }
    }
}
