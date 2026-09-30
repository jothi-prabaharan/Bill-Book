using Microsoft.EntityFrameworkCore.Migrations;

namespace Shared.Kernel.Audit;

/// <summary>
/// The hand-written half of a schema's <c>EditLog</c> (TK-118): the tenant RLS
/// policy, and the trigger that makes the table append-only. Every tenant
/// schema's migration calls <see cref="Up"/> after creating the table.
///
/// <b>Carry these calls forward by hand in any future squash.</b> The model knows
/// nothing of triggers or policies, so a regenerated migration drops them
/// silently — which is how the RLS policies were lost once already.
/// </summary>
public static class EditLogMigration
{
    public static void Up(MigrationBuilder migrationBuilder, string schema)
    {
        migrationBuilder.Sql($"""
            ALTER TABLE {schema}."EditLog" ENABLE ROW LEVEL SECURITY;
            ALTER TABLE {schema}."EditLog" FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS editlog_tenant_isolation ON {schema}."EditLog";
            CREATE POLICY editlog_tenant_isolation
                ON {schema}."EditLog"
                FOR ALL
                USING (
                    "CustomerId" = NULLIF(current_setting('app.current_customer_id', true), '')::uuid
                    AND "OrgId" = NULLIF(current_setting('app.current_org_id', true), '')::uuid);
            """);

        // Append-only: no row is ever changed or removed, by the application or
        // anyone else connecting as it. TRUNCATE is a statement-level event of its
        // own, so it needs its own trigger.
        migrationBuilder.Sql($$"""
            CREATE OR REPLACE FUNCTION {{schema}}.editlog_append_only() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'The edit log is append-only: % on %.EditLog is refused.', TG_OP, TG_TABLE_SCHEMA
                    USING ERRCODE = 'insufficient_privilege';
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS trg_editlog_append_only ON {{schema}}."EditLog";
            CREATE TRIGGER trg_editlog_append_only
                BEFORE UPDATE OR DELETE ON {{schema}}."EditLog"
                FOR EACH ROW EXECUTE FUNCTION {{schema}}.editlog_append_only();

            DROP TRIGGER IF EXISTS trg_editlog_no_truncate ON {{schema}}."EditLog";
            CREATE TRIGGER trg_editlog_no_truncate
                BEFORE TRUNCATE ON {{schema}}."EditLog"
                FOR EACH STATEMENT EXECUTE FUNCTION {{schema}}.editlog_append_only();
            """);
    }

    public static void Down(MigrationBuilder migrationBuilder, string schema)
    {
        migrationBuilder.Sql($"""
            DROP TRIGGER IF EXISTS trg_editlog_no_truncate ON {schema}."EditLog";
            DROP TRIGGER IF EXISTS trg_editlog_append_only ON {schema}."EditLog";
            DROP FUNCTION IF EXISTS {schema}.editlog_append_only();
            """);
    }
}
