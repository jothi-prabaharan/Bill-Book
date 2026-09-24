using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Printing.Repository.Migrations
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recreates both prt policies with the expression every other schema
            // now uses (TK-05 in docs/TASKS.md). The initial migration cast
            // current_setting(...)::uuid directly, and the tenant interceptor
            // sets '' when a request has no tenant, so such a request threw
            // "invalid input syntax for type uuid" instead of seeing no rows.
            // NULLIF turns '' into NULL, and a NULL comparison matches nothing.
            //
            // Same policy names as before, so nothing that reads pg_policies by
            // name has to change. ENABLE and FORCE are repeated so the block is
            // whole on its own, the way TK-71's template writes it.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    ALTER TABLE prt."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE prt."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON prt."{table}";
                    CREATE POLICY {policy}
                        ON prt."{table}"
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
            // Back to the initial migration's expression, without NULLIF.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {policy} ON prt."{table}";
                    CREATE POLICY {policy}
                        ON prt."{table}"
                        FOR ALL
                        USING (
                            "CustomerId" = current_setting('app.current_customer_id', true)::uuid
                            AND "OrgId" = current_setting('app.current_org_id', true)::uuid);
                    """);
            }
        }

        /// <summary>Both <c>prt</c> tables; each carries <c>CustomerId</c> and <c>OrgId</c>.</summary>
        private static readonly string[] Tables =
        [
            "PrintTemplates",
            "ErrorLogs",
        ];
    }
}
