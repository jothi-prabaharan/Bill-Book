using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class ApplicationsMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1085,
                columns: new[] { "IsActive", "Name", "RoutePath" },
                values: new object[] { true, "Applications", "/settings/applications" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1085,
                columns: new[] { "IsActive", "Name", "RoutePath" },
                values: new object[] { false, "Licences", null });
        }
    }
}
