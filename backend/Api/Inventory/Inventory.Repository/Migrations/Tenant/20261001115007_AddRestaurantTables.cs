using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRestaurantTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE inv.\"RestaurantTables\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"RestaurantTables\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"RestaurantTables\";");


        }
    }
}

