using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <summary>
    /// The one <c>sal</c> table that never got a row-level security policy.
    ///
    /// <c>ForceRls</c> covered the fifteen document tables and stopped there;
    /// <c>sal.SalesRegister</c> was added separately and missed the loop. It is an
    /// <c>OrgScopedEntity</c> like the rest, so the EF query filter applies to it —
    /// but the filter is a property of the code, and one query written with
    /// <c>IgnoreQueryFilters</c> would have had nothing behind it.
    ///
    /// It is also the table GSTR-1 is filed from, which makes it the worst one in
    /// the schema to leave with a single guard.
    ///
    /// <c>Sales.Api.Tests.SalesQueryFilterTests.Row_level_security_covers_every_table_in_the_schema</c>
    /// asserts this over <c>pg_tables</c> rather than over one table a test happens
    /// to touch, so a future table added without a policy fails the suite.
    /// </summary>
    /// <inheritdoc />
    public partial class AddSalesRegisterRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE sal.\"SalesRegister\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS salesregister_org_isolation ON sal.\"SalesRegister\";");
            migrationBuilder.Sql(
                "CREATE POLICY salesregister_org_isolation ON sal.\"SalesRegister\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS salesregister_org_isolation ON sal.\"SalesRegister\";");
            migrationBuilder.Sql("ALTER TABLE sal.\"SalesRegister\" DISABLE ROW LEVEL SECURITY;");
        }
    }
}
