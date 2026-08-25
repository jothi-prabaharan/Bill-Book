using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PriceLists/PriceListItems had a policy but RLS was never actually
            // enabled on the table (ENABLE ROW LEVEL SECURITY was missing from
            // the migration that added them) — a dead policy, same shape as the
            // sal.SalesRegister gap. Closed here since every table's policy is
            // being rewritten anyway; ENABLE/FORCE is idempotent on the other 18.
            foreach (string table in new[]
            {
                "Items",
                "ItemBarcodes",
                "ItemBatches",
                "ItemCategories",
                "ItemJewelleryDetails",
                "ItemPharmaDetails",
                "ItemSerials",
                "ItemStock",
                "MetalPurities",
                "UomTypes",
                "UnitOfMeasures",
                "Warehouses",
                "StockAdjustments",
                "StockAdjustmentLines",
                "StockMovements",
                "CostLayers",
                "CostLayerConsumptions",
                "RecostingAdjustments",
                "PriceLists",
                "PriceListItems",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE inv.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE inv.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\";");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS tenant_isolation_policy ON inv.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON inv.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Items",
                "ItemBarcodes",
                "ItemBatches",
                "ItemCategories",
                "ItemJewelleryDetails",
                "ItemPharmaDetails",
                "ItemSerials",
                "ItemStock",
                "MetalPurities",
                "UomTypes",
                "UnitOfMeasures",
                "Warehouses",
                "StockAdjustments",
                "StockAdjustmentLines",
                "StockMovements",
                "CostLayers",
                "CostLayerConsumptions",
                "RecostingAdjustments",
                "PriceLists",
                "PriceListItems",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON inv.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON inv.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
