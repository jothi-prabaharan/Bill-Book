using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class ProjectReportMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "mst",
                table: "Menus",
                columns: new[] { "MenuId", "Apps", "CanCreate", "Code", "CreatedAt", "CreatedBy", "DisplayOrder", "Icon", "IsActive", "IsSearchable", "ModifiedAt", "ModifiedBy", "Module", "Name", "ParentId", "RoutePath", "SingularName", "Type" },
                values: new object[,]
                {
                    { 123, 1, false, "reports-g8", null, null, 8, "briefcase", true, false, null, null, null, "Projects", 8, null, null, "Group" },
                    { 1129, 1, false, "r-project-profitability", null, null, 1, "trending-up", true, true, null, null, "reports", "Project Profitability", 123, "/reports/project-profitability", null, "Item" },
                    { 1130, 1, false, "r-project-budget-vs-actual", null, null, 2, "target", true, true, null, null, "reports", "Project Budget vs Actual", 123, "/reports/project-budget-vs-actual", null, "Item" },
                    { 1131, 1, false, "r-project-unbilled-work", null, null, 3, "receipt", true, true, null, null, "reports", "Project Unbilled Work", 123, "/reports/project-unbilled-work", null, "Item" },
                    { 1132, 1, false, "r-project-time-by-user", null, null, 4, "clock", true, true, null, null, "reports", "Project Time by User", 123, "/reports/project-time-by-user", null, "Item" }
                });

            migrationBuilder.InsertData(
                schema: "mst",
                table: "MenuPermissions",
                columns: new[] { "MenuPermissionId", "Action", "CreatedAt", "CreatedBy", "MenuId", "ModifiedAt", "ModifiedBy", "Module", "PermissionCode" },
                values: new object[,]
                {
                    { 455, "view", null, null, 1129, null, null, "reports", "reports.view" },
                    { 456, "export", null, null, 1129, null, null, "reports", "reports.export" },
                    { 457, "view", null, null, 1130, null, null, "reports", "reports.view" },
                    { 458, "export", null, null, 1130, null, null, "reports", "reports.export" },
                    { 459, "view", null, null, 1131, null, null, "reports", "reports.view" },
                    { 460, "export", null, null, 1131, null, null, "reports", "reports.export" },
                    { 461, "view", null, null, 1132, null, null, "reports", "reports.view" },
                    { 462, "export", null, null, 1132, null, null, "reports", "reports.export" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 455);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 456);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 457);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 458);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 459);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 460);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 461);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "MenuPermissions",
                keyColumn: "MenuPermissionId",
                keyValue: 462);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1129);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1130);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1131);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1132);

            migrationBuilder.DeleteData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 123);
        }
    }
}
