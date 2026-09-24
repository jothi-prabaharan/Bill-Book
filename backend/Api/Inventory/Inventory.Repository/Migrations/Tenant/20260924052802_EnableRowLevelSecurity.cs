using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Inventory.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security for inv, copied from acc's
            // EnableRowLevelSecurity (TK-74; the template is TK-71), which explains
            // every clause. In short: FORCE because the application owns these
            // tables; FOR ALL with USING only, so the same test guards writes;
            // NULLIF so a request with no tenant sees nothing rather than
            // throwing on ''::uuid.
            //
            // CostingEngine.Worker has no request to take a tenant from. It lists branches
            // from Master (mst, which carries no RLS) and sets each branch's tenant on its
            // own scope before touching inv, so it keeps costing under these policies.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE inv."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE inv."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON inv."{table}";
                    CREATE POLICY {policy}
                        ON inv."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                            AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in Tables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON inv."{table}";
                    ALTER TABLE inv."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE inv."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        /// <summary>
        /// Every table in <c>inv</c> as of this migration, all 21 of them. None is
        /// exempt: each carries <c>CustomerId</c> and <c>OrgId</c>.
        /// </summary>
        private static readonly string[] Tables =
        [
            "CostLayerConsumptions",
            "CostLayers",
            "ErrorLogs",
            "ItemBarcodes",
            "ItemBatches",
            "ItemCategories",
            "ItemJewelleryDetails",
            "ItemPharmaDetails",
            "ItemSerials",
            "ItemStock",
            "Items",
            "MetalPurities",
            "PriceListItems",
            "PriceLists",
            "RecostingAdjustments",
            "StockAdjustmentLines",
            "StockAdjustments",
            "StockMovements",
            "UnitOfMeasures",
            "UomTypes",
            "Warehouses",
        ];
    }
}
