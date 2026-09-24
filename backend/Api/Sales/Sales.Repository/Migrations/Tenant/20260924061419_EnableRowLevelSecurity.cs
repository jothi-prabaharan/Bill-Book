using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security for sal, copied from TK-71's template in
            // Accounting.Repository/Migrations/Tenant/20260923173706_EnableRowLevelSecurity.cs,
            // where the reasoning is written out: FORCE because the application
            // connects as the table owner, USING doubling as the write check,
            // and NULLIF so a request with no tenant sees nothing rather than
            // throwing on ''::uuid. TK-03 in docs/TASKS.md.
            //
            // SalesRegister is on the list. It is the table GSTR-1 is filed from,
            // and it was once left out of the RLS loop with no guard at all.
            // Notification.Worker reads sal with no tenant set, so under this
            // policy it sees nothing until TK-20 gives it one per branch.
            //
            // The list is written out, not derived at run time: a migration has
            // to do the same thing forever. A table added to sal later needs
            // its own migration with this block, and RlsAudit fails until it has one.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE sal."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE sal."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON sal."{table}";
                    CREATE POLICY {policy}
                        ON sal."{table}"
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
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON sal."{table}";
                    ALTER TABLE sal."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE sal."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        /// <summary>
        /// Every table in <c>sal</c> as of this migration, all 19 of them. None is
        /// exempt: each carries <c>CustomerId</c> and <c>OrgId</c>. <c>NumberingSeries</c>
        /// is mapped here but belongs to <c>acc</c>, whose own migration covers it.
        /// </summary>
        private static readonly string[] Tables =
        [
            "CreditNoteDetailTaxes",
            "CreditNoteDetails",
            "CreditNotes",
            "DeliveryChallanDetailTaxes",
            "DeliveryChallanDetails",
            "DeliveryChallans",
            "ErrorLogs",
            "InvoiceDetailTaxes",
            "InvoiceDetails",
            "Invoices",
            "QuoteDetailTaxes",
            "QuoteDetails",
            "Quotes",
            "ReminderLogs",
            "ReminderProfiles",
            "SalesOrderDetailTaxes",
            "SalesOrderDetails",
            "SalesOrders",
            "SalesRegister",
        ];
    }
}
