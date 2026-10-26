using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_05_InvoiceTaxReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-224): sal.Invoices and sal.CreditNotes are Sales' tables,
            // excluded from migrations. The read models now also carry the taxable amount
            // and the CGST, SGST, IGST and cess the headers already hold, and the credit
            // note its currency and rate; this migration only moves the snapshot, so the
            // model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
