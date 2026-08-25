using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Quotes",
                "QuoteDetails",
                "QuoteDetailTaxes",
                "SalesOrders",
                "SalesOrderDetails",
                "SalesOrderDetailTaxes",
                "Invoices",
                "InvoiceDetails",
                "InvoiceDetailTaxes",
                "DeliveryChallans",
                "DeliveryChallanDetails",
                "DeliveryChallanDetailTaxes",
                "CreditNotes",
                "CreditNoteDetails",
                "CreditNoteDetailTaxes",
                "SalesRegister",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE sal.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE sal.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON sal.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Quotes",
                "QuoteDetails",
                "QuoteDetailTaxes",
                "SalesOrders",
                "SalesOrderDetails",
                "SalesOrderDetailTaxes",
                "Invoices",
                "InvoiceDetails",
                "InvoiceDetailTaxes",
                "DeliveryChallans",
                "DeliveryChallanDetails",
                "DeliveryChallanDetailTaxes",
                "CreditNotes",
                "CreditNoteDetails",
                "CreditNoteDetailTaxes",
                "SalesRegister",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON sal.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
