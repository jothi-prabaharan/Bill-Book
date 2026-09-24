using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Row-level security for cus, copied from acc's
            // EnableRowLevelSecurity (TK-04; the template is TK-02), which explains
            // every clause. In short: FORCE because the application owns these
            // tables; FOR ALL with USING only, so the same test guards writes;
            // NULLIF so a request with no tenant sees nothing rather than
            // throwing on ''::uuid.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE cus."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE cus."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON cus."{table}";
                    CREATE POLICY {policy}
                        ON cus."{table}"
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
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON cus."{table}";
                    ALTER TABLE cus."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE cus."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }

        /// <summary>
        /// Every table in <c>cus</c> as of this migration, all 4 of them. None is
        /// exempt: each carries <c>CustomerId</c> and <c>OrgId</c>.
        /// </summary>
        private static readonly string[] Tables =
        [
            "ErrorLogs",
            "Leads",
            "TicketMessages",
            "Tickets",
        ];
    }
}
