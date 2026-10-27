using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_14_PayableInvoiceDetailReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BillDetailTaxRead is a new read model over pur.BillDetailTaxes, which Purchase owns:
            // nothing in the database changes, only the model snapshot (TK-236).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
