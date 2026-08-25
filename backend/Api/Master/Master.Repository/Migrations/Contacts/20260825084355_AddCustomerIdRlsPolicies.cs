using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Contacts
{
    /// <inheritdoc />
    public partial class AddCustomerIdRlsPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ApiClients was added after RestoreContactRls and never got RLS —
            // closed here rather than left for a separate migration, since this
            // is already rewriting every policy on this schema's table list.
            foreach (string table in new[]
            {
                "Contacts",
                "ContactAddresses",
                "ContactPersons",
                "ContactPersonRoles",
                "ContactBankDetails",
                "ContactLicences",
                "ContactAttachments",
                "ApiClients"
            })
            {
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_tenant_isolation ON con.\"{table}\" " +
                    "USING (\"CustomerId\" = current_setting('app.current_customer_id', true)::uuid " +
                    "AND \"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Contacts",
                "ContactAddresses",
                "ContactPersons",
                "ContactPersonRoles",
                "ContactBankDetails",
                "ContactLicences",
                "ContactAttachments",
                "ApiClients"
            })
            {
                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_tenant_isolation ON con.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }
    }
}
