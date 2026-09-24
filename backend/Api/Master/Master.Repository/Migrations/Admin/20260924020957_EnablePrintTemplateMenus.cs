using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class EnablePrintTemplateMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/QTE" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/SOR" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/DLC" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/INV" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/CRN" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/POR" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/GRN" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/BIL" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/DBN" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/RCM" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/SPM" });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { true, "/settings/print-templates/JRN" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1094,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1095,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1096,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1097,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1098,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1099,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1100,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1101,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1102,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1103,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1104,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1105,
                columns: new[] { "IsActive", "RoutePath" },
                values: new object[] { false, null });
        }
    }
}
