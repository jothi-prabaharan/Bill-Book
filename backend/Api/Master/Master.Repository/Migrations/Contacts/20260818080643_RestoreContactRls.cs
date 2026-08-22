using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Contacts
{
    public partial class RestoreContactRls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (string table in new[]
            {
                "Contacts",
                "ContactAddresses",
                "ContactPersons",
                "ContactPersonRoles",
                "ContactBankDetails",
                "ContactLicences",
                "ContactAttachments"
            })
            {
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE con.\"{table}\" FORCE ROW LEVEL SECURITY;");

                migrationBuilder.Sql(
                    $"DROP POLICY IF EXISTS {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\";");

                migrationBuilder.Sql(
                    $"CREATE POLICY {table.ToLowerInvariant()}_org_isolation ON con.\"{table}\" " +
                    "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
