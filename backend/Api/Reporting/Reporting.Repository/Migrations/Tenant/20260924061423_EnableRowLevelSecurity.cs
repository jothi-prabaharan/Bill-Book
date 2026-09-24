using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class EnableRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replaces the policies the initial migration wrote, which were
            // broken rather than missing (TK-04 in docs/TASKS.md). They compared
            // "OrgId" with current_setting('tenant.orgid'), a setting nothing in
            // this codebase sets, and ignored CustomerId. With FORCE on and a
            // connection that does not bypass RLS, all four tables showed no
            // rows to anyone: a branch could not list its own reports. It went
            // unseen because every developer and CI connection is a superuser.
            //
            // The replacement is TK-71's template, verbatim — see
            // Accounting.Repository/Migrations/Tenant/20260923173706_EnableRowLevelSecurity.cs
            // for the reasoning behind FORCE, USING without WITH CHECK, and NULLIF.
            //
            // ReportMasters and ReportColumns are left without a policy on
            // purpose. They hold the imported reports.json specification, shared
            // by every customer, and carry no tenant column to compare. RlsAudit
            // names them as its exemption.
            foreach (string table in Tables)
            {
                string policy = $"{table.ToLowerInvariant()}_tenant_isolation";

                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS "TenantPolicy" ON rpt."{table}";
                    ALTER TABLE rpt."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE rpt."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {policy} ON rpt."{table}";
                    CREATE POLICY {policy}
                        ON rpt."{table}"
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
            // Back to what the initial migration left, broken policy included:
            // a Down restores the previous state, it does not improve on it.
            foreach (string table in Tables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON rpt."{table}";
                    CREATE POLICY "TenantPolicy" ON rpt."{table}" AS PERMISSIVE FOR ALL TO public USING ("OrgId" = current_setting('tenant.orgid', true)::uuid);
                    """);
            }
        }

        /// <summary>
        /// The four <c>rpt</c> tables that carry <c>CustomerId</c> and <c>OrgId</c>.
        /// <c>ReportMasters</c> and <c>ReportColumns</c> carry neither and are exempt.
        /// </summary>
        private static readonly string[] Tables =
        [
            "ErrorLogs",
            "ReportDetails",
            "ReportViews",
            "Reports",
        ];
    }
}
