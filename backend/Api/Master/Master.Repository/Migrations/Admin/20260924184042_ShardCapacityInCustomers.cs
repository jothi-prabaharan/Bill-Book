using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class ShardCapacityInCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxOrganizations",
                schema: "mst",
                table: "TenantDatabases",
                newName: "MaxCustomers");

            migrationBuilder.RenameColumn(
                name: "CurrentOrganizations",
                schema: "mst",
                table: "TenantDatabases",
                newName: "CurrentCustomers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MaxCustomers",
                schema: "mst",
                table: "TenantDatabases",
                newName: "MaxOrganizations");

            migrationBuilder.RenameColumn(
                name: "CurrentCustomers",
                schema: "mst",
                table: "TenantDatabases",
                newName: "CurrentOrganizations");
        }
    }
}
