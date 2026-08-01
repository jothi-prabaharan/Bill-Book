using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <summary>
    /// Row-level security for the three Accounting tables that never had it.
    ///
    /// <c>acc.Accounts</c>, <c>acc.SubAccounts</c> and <c>acc.TaxMasters</c> were
    /// the only per-customer tables in the system relying on the EF query filter
    /// alone. Every other table in every other schema — including this schema's
    /// own <c>NumberingSeries</c> and <c>PaymentTerms</c> — already carries a
    /// policy. These three were simply missed.
    ///
    /// The query filter is the first line of defence, not the last. It is a
    /// property of the code: a single query written with <c>IgnoreQueryFilters</c>,
    /// a raw command, or a context built without a tenant, and a branch reads
    /// another branch's chart of accounts. The policy is a property of the
    /// database, and holds however the connection is used.
    ///
    /// Chart of accounts and tax rates are exactly the tables where that matters:
    /// they are read by nearly every posting path in the product.
    /// </summary>
    public partial class AddAccountingRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach ((string table, string policy) in new[]
            {
                ("Accounts", "accounts_org_isolation"),
                ("SubAccounts", "sub_accounts_org_isolation"),
                ("TaxMasters", "tax_masters_org_isolation"),
            })
            {
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" ENABLE ROW LEVEL SECURITY;");

                // Dropped first so the migration is safe to re-run against a
                // database where it was applied by hand.
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON acc.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {policy} ON acc.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach ((string table, string policy) in new[]
            {
                ("TaxMasters", "tax_masters_org_isolation"),
                ("SubAccounts", "sub_accounts_org_isolation"),
                ("Accounts", "accounts_org_isolation"),
            })
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS {policy} ON acc.\"{table}\";");
                migrationBuilder.Sql($"ALTER TABLE acc.\"{table}\" DISABLE ROW LEVEL SECURITY;");
            }
        }
    }
}
