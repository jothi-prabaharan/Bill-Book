using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security for pur, copied from TK-71's template in
            // Accounting.Repository/Migrations/Tenant/20260923173706_EnableRowLevelSecurity.cs,
            // where the reasoning is written out: FORCE because the application
            // connects as the table owner, USING doubling as the write check,
            // and NULLIF so a request with no tenant sees nothing rather than
            // throwing on ''::uuid. TK-02 in docs/TASKS.md.
            //
            // The list is written out, not derived at run time: a migration has
            // to do the same thing forever. A table added to pur later needs
            // its own migration with this block, and RlsAudit fails until it has one.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE pur."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE pur."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON pur."{table}";
                    CREATE POLICY {policy}
                        ON pur."{table}"
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
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON pur."{table}";
                    ALTER TABLE pur."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE pur."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        /// <summary>
        /// Every table in <c>pur</c> as of this migration, all 13 of them. None is
        /// exempt: each carries <c>CustomerId</c> and <c>OrgId</c>. <c>NumberingSeries</c>
        /// is mapped here but belongs to <c>acc</c>, whose own migration covers it.
        /// </summary>
        private static readonly string[] Tables =
        [
            "BillDetailTaxes",
            "BillDetails",
            "Bills",
            "DebitNoteDetailTaxes",
            "DebitNoteDetails",
            "DebitNotes",
            "ErrorLogs",
            "GoodsReceiptDetailTaxes",
            "GoodsReceiptDetails",
            "GoodsReceipts",
            "PurchaseOrderDetailTaxes",
            "PurchaseOrderDetails",
            "PurchaseOrders",
        ];
    }
}
