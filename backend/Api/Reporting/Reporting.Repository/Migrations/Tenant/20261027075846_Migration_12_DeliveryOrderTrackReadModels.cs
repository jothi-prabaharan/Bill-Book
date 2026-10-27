using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_12_DeliveryOrderTrackReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Read models only (TK-232): DeliveryChallanDetailRead and InvoiceRead.DeliveryChallanId map tables and columns that already exist in the sal schema.
            // Nothing to change in the database; this migration only brings the model snapshot up to date.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
