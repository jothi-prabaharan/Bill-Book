using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Quotes", "QuoteDetails", "QuoteDetailTaxes",
                "SalesOrders", "SalesOrderDetails", "SalesOrderDetailTaxes",
                "DeliveryChallans", "DeliveryChallanDetails", "DeliveryChallanDetailTaxes",
                "Invoices", "InvoiceDetails", "InvoiceDetailTaxes",
                "CreditNotes", "CreditNoteDetails", "CreditNoteDetailTaxes",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE sal.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\";");
                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "CreditNoteDetailTaxes", "CreditNoteDetails", "CreditNotes",
                "InvoiceDetailTaxes", "InvoiceDetails", "Invoices",
                "DeliveryChallanDetailTaxes", "DeliveryChallanDetails", "DeliveryChallans",
                "SalesOrderDetailTaxes", "SalesOrderDetails", "SalesOrders",
                "QuoteDetailTaxes", "QuoteDetails", "Quotes",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON sal.\"{table}\";");
            }
        }
    }
}
