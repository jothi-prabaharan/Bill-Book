using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_18_PurchaseOrderTrackReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TK-240: PurchaseOrderDetailRead is mapped for the Purchase Order Track Report (pur.PurchaseOrderDetails).
            // A read model of a table Purchase owns, excluded from migrations: the snapshot changes and the schema does not.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
