using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class FixRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS \"tenant_isolation_policy\" ON inv.\"PriceLists\";");
            migrationBuilder.Sql("CREATE POLICY \"tenant_isolation_policy\" ON inv.\"PriceLists\" USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            
            migrationBuilder.Sql("DROP POLICY IF EXISTS \"tenant_isolation_policy\" ON inv.\"PriceListItems\";");
            migrationBuilder.Sql("CREATE POLICY \"tenant_isolation_policy\" ON inv.\"PriceListItems\" USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
