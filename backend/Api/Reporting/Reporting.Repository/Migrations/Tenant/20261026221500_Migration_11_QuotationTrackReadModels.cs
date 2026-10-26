using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_11_QuotationTrackReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Read models only (TK-231): QuoteRead.CustomerResponse and InvoiceRead.QuoteId map columns that already exist in the sal schema.
            // Nothing to change in the database; this migration only brings the model snapshot up to date.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
