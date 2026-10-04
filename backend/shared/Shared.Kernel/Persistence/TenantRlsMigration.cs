using Microsoft.EntityFrameworkCore.Migrations;

namespace Shared.Kernel.Persistence;

/// <summary>
/// The hand-written RLS block every tenant table needs, in TK-71's form: enabled,
/// FORCEd, and one policy on <c>CustomerId</c> and <c>OrgId</c> with
/// <c>NULLIF</c>, so "no tenant" sees nothing rather than throwing on
/// <c>''::uuid</c>.
///
/// Call it <b>after</b> the table is created. <b>Carry the call forward by hand
/// in any future squash</b>: the model knows nothing of policies, which is how
/// they were lost once already.
/// </summary>
public static class TenantRlsMigration
{
    public static void Enable(MigrationBuilder migrationBuilder, string schema, string table)
    {
        string policy = PolicyName(table);
        migrationBuilder.Sql($"""
            ALTER TABLE {schema}."{table}" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {schema}."{table}" FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS {policy} ON {schema}."{table}";
            CREATE POLICY {policy}
                ON {schema}."{table}"
                FOR ALL
                USING (
                    "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                    AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
            """);
    }

    /// <summary>For a <c>Down</c> that keeps the table; dropping the table drops its policy anyway.</summary>
    public static void Disable(MigrationBuilder migrationBuilder, string schema, string table)
    {
        migrationBuilder.Sql($"""
            DROP POLICY IF EXISTS {PolicyName(table)} ON {schema}."{table}";
            ALTER TABLE {schema}."{table}" NO FORCE ROW LEVEL SECURITY;
            ALTER TABLE {schema}."{table}" DISABLE ROW LEVEL SECURITY;
            """);
    }

    public static string PolicyName(string table) => $"{table.ToLowerInvariant()}_tenant_isolation";
}
