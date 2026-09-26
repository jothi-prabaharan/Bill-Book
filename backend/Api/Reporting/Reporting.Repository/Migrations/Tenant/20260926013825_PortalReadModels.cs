using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class PortalReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to apply: every change is to read models Reporting maps
            // with ExcludeFromMigrations (TK-95) — the ledger's DocumentNo,
            // invoices' base total, credit notes, and document statuses read as
            // their stored names. The snapshot records them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
