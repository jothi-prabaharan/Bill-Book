using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRestaurantMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE inv.\"MenuCategories\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"MenuCategories\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"MenuSchedules\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"MenuSchedules\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"MenuItems\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"MenuItems\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"ItemVariants\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"ItemVariants\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"ModifierGroups\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"ModifierGroups\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"Modifiers\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"Modifiers\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");
            migrationBuilder.Sql("ALTER TABLE inv.\"MenuItemPrices\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("CREATE POLICY \"TenantIsolation\" ON inv.\"MenuItemPrices\" FOR ALL USING (\"OrgId\" = current_setting('app.current_org_id')::uuid);");


        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"MenuCategories\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"MenuSchedules\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"MenuItems\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"ItemVariants\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"ModifierGroups\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"Modifiers\";");
            migrationBuilder.Sql("DROP POLICY \"TenantIsolation\" ON inv.\"MenuItemPrices\";");


        }
    }
}


