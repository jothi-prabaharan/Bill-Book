using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class ProjectReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to apply: every change is to read models Reporting maps
            // with ExcludeFromMigrations (TK-107) — acc's project tables, the
            // ledger's ProjectId, and the billing columns on spend-money and
            // bill lines. The snapshot records them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
