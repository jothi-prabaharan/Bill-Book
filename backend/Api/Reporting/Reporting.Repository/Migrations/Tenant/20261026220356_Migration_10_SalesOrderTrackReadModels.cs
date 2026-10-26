using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_10_SalesOrderTrackReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-230): sal.SalesOrderDetails and sal.Invoices are Sales' tables, excluded from migrations.
            // The read models now also carry the line's InvoicedQuantity and the invoice's SalesOrderId, which the tables
            // already hold; this migration only moves the snapshot, so the model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
