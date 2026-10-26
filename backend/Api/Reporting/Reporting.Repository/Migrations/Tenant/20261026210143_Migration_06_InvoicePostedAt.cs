using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_06_InvoicePostedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-226): sal.Invoices is Sales' table, excluded from migrations.
            // The read model now also carries PostedAt, which the header already holds; this
            // migration only moves the snapshot, so the model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
