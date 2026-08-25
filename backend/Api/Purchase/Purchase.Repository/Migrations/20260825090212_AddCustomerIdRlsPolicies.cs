using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "PurchaseOrders",
                "PurchaseOrderDetails",
                "PurchaseOrderDetailTaxes",
                "GoodsReceipts",
                "GoodsReceiptDetails",
                "GoodsReceiptDetailTaxes",
                "Bills",
                "BillDetails",
                "BillDetailTaxes",
                "DebitNotes",
                "DebitNoteDetails",
                "DebitNoteDetailTaxes",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE pur.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE pur.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON pur.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON pur.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "PurchaseOrders",
                "PurchaseOrderDetails",
                "PurchaseOrderDetailTaxes",
                "GoodsReceipts",
                "GoodsReceiptDetails",
                "GoodsReceiptDetailTaxes",
                "Bills",
                "BillDetails",
                "BillDetailTaxes",
                "DebitNotes",
                "DebitNoteDetails",
                "DebitNoteDetailTaxes",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON pur.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON pur.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
