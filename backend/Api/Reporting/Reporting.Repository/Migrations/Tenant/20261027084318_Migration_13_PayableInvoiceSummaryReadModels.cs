using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_13_PayableInvoiceSummaryReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Read models only (TK-235): BillRead's tax, base total, reverse-charge and TDS properties and the new DebitNoteRead map columns and a table that already exist in the pur schema.
            // Nothing to change in the database; this migration only brings the model snapshot up to date.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
