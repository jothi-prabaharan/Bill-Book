using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ReportMasters/ReportColumns are deliberately excluded: they are the
            // report catalog, plain (not OrgScopedEntity) global reference data
            // shared by every customer, like mst.AccountTypes — no OrgId or
            // CustomerId column exists on them to filter by.
            foreach (string table in new[] { "Reports", "ReportViews", "ReportDetails" })
            {
                migrationBuilder.Sql($"ALTER TABLE rpt.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE rpt.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON rpt.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON rpt.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[] { "Reports", "ReportViews", "ReportDetails" })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON rpt.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON rpt.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
