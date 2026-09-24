using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security for con, copied from acc's EnableRowLevelSecurity
            // (TK-71), which explains every clause. In short: FORCE because the
            // application owns these tables; FOR ALL with USING only, so the same
            // test guards writes; NULLIF so a request with no tenant sees nothing
            // rather than throwing on ''::uuid.
            foreach (string table in Tables)
            {
                migrationBuilder.Sql(Enable(table, $"""
                    "CustomerId" = {CurrentCustomer}
                    AND "OrgId" = {CurrentOrg}
                    """));
            }

            // ApiClients is the one table read before a branch is known. An API
            // key names its customer (bb_{customer}_{secret}) but not its branch,
            // so InternalApiKeysController sets only the customer, fetches that
            // customer's active keys and BCrypt-verifies them in memory. The
            // branch is what the matched row tells it.
            //
            // So a request with a customer and no branch sees every branch of
            // that customer here. A request with a branch set, which is every
            // staff request, is held to its branch exactly as on every other
            // table. Nothing crosses customers. The alternatives were worse: an
            // exempt table has no second guard at all, and carrying the branch
            // in the key would change a format already issued to clients.
            migrationBuilder.Sql(Enable("ApiClients", $"""
                "CustomerId" = {CurrentCustomer}
                AND ("OrgId" = {CurrentOrg} OR {CurrentOrg} IS NULL)
                """));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in Tables.Append("ApiClients"))
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON con."{table}";
                    ALTER TABLE con."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE con."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        private const string CurrentCustomer =
            "NULLIF(current_setting('app.current_customer_id', true), '')::uuid";

        private const string CurrentOrg =
            "NULLIF(current_setting('app.current_org_id', true), '')::uuid";

        private static string Enable(string table, string condition)
        {
            string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

            return $"""
                ALTER TABLE con."{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE con."{table}" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS {policy} ON con."{table}";
                CREATE POLICY {policy}
                    ON con."{table}"
                    FOR ALL
                    USING ({condition});
                """;
        }

        /// <summary>
        /// Every table in <c>con</c> as of this migration except <c>ApiClients</c>,
        /// which gets its own policy above. All ten carry <c>CustomerId</c> and
        /// <c>OrgId</c>; none is exempt.
        /// </summary>
        private static readonly string[] Tables =
        [
            "ContactAddresses",
            "ContactAttachments",
            "ContactBankDetails",
            "ContactLicences",
            "ContactPersonRoles",
            "ContactPersons",
            "Contacts",
            "ErrorLogs",
            "PrintTemplates",
        ];
    }
}
