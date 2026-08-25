using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NumberingSeries has never had an RLS policy at all — it is
            // OrgScopedEntity but was never added to a *Rls migration, so it
            // relied solely on the EF query filter. Closed here alongside
            // everything else, since every policy on this schema is being
            // rewritten anyway. The four fixed-asset tables used the single name
            // "TenantPolicy" rather than the schema's usual {table}_org_isolation
            // — both old names are dropped so this runs cleanly either way.
            foreach (string table in new[]
            {
                "Accounts",
                "SubAccounts",
                "TaxMasters",
                "PaymentTerms",
                "NumberingSeries",
                "Journals",
                "JournalDetails",
                "JournalLedger",
                "PeriodLocks",
                "OpeningBalances",
                "OpeningBalanceLines",
                "Banks",
                "BankAccounts",
                "SpendMoney",
                "SpendMoneyDetails",
                "ReceiveMoney",
                "ReceiveMoneyDetails",
                "TransferMoney",
                "BankStatements",
                "BankStatementLines",
                "StatementImportProfiles",
                "TransactionRatios",
                "FixedAssetCategories",
                "FixedAssets",
                "DepreciationSchedules",
                "AssetTransactions",
            })
            {
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\";");
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS \"TenantPolicy\" ON acc.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON acc.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Accounts",
                "SubAccounts",
                "TaxMasters",
                "PaymentTerms",
                "NumberingSeries",
                "Journals",
                "JournalDetails",
                "JournalLedger",
                "PeriodLocks",
                "OpeningBalances",
                "OpeningBalanceLines",
                "Banks",
                "BankAccounts",
                "SpendMoney",
                "SpendMoneyDetails",
                "ReceiveMoney",
                "ReceiveMoneyDetails",
                "TransferMoney",
                "BankStatements",
                "BankStatementLines",
                "StatementImportProfiles",
                "TransactionRatios",
                "FixedAssetCategories",
                "FixedAssets",
                "DepreciationSchedules",
                "AssetTransactions",
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON acc.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON acc.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
