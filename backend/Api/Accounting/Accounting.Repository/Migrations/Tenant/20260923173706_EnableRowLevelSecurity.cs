using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security, written by hand because EF Core generates none
            // of it. The chain squashed on 14 September 2026 dropped the blocks
            // that used to do this, and nothing noticed: the developer databases
            // still carried the old policies. TK-02 in docs/TASKS.md restores it,
            // and this migration is the template TK-03 to TK-08 copy.
            //
            // The EF query filter on TenantDbContext and this policy say the same
            // thing on purpose. Neither is trusted alone: a query that escapes the
            // filter (IgnoreQueryFilters, a raw command, a context that forgot
            // base.OnModelCreating) still meets this.
            //
            // FORCE is what makes it real. Without it RLS does not apply to the
            // table's owner, and the application connects as that owner.
            //
            // FOR ALL with USING and no WITH CHECK: Postgres reuses USING as the
            // check on INSERT and UPDATE, so a row cannot be written into another
            // branch either.
            //
            // NULLIF(..., '') is the one change from the old policy. The tenant
            // interceptor sets '' when a request has no tenant, and ''::uuid
            // throws. With NULLIF the comparison is NULL instead, so a request
            // with no tenant sees no rows and cannot write, rather than failing
            // with an error that names the setting. current_setting(..., true)
            // returns NULL when the setting was never set at all, which lands in
            // the same place.
            //
            // The list is written out, not derived at run time: a migration has
            // to do the same thing forever, and every table here carries both
            // CustomerId and OrgId. A table added to acc later needs its own
            // migration with this block. RlsAudit fails until it has one.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE acc."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE acc."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON acc."{table}";
                    CREATE POLICY {policy}
                        ON acc."{table}"
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
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON acc."{table}";
                    ALTER TABLE acc."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE acc."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        /// <summary>
        /// Every table in <c>acc</c> as of this migration, all 27 of them. None is
        /// exempt: each carries <c>CustomerId</c> and <c>OrgId</c>.
        /// </summary>
        private static readonly string[] Tables =
        [
            "Accounts",
            "AssetTransactions",
            "BankAccounts",
            "BankStatementLines",
            "BankStatements",
            "Banks",
            "DepreciationSchedules",
            "ErrorLogs",
            "FixedAssetCategories",
            "FixedAssets",
            "JournalDetails",
            "JournalLedger",
            "Journals",
            "NumberingSeries",
            "OpeningBalanceLines",
            "OpeningBalances",
            "PaymentTerms",
            "PeriodLocks",
            "ReceiveMoney",
            "ReceiveMoneyDetails",
            "SpendMoney",
            "SpendMoneyDetails",
            "StatementImportProfiles",
            "SubAccounts",
            "TaxMasters",
            "TransactionRatios",
            "TransferMoney",
        ];
    }
}
