using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_07_ReceiveMoneyTds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-227): acc.ReceiveMoney is Accounting's table, excluded from migrations.
            // The read model now also carries TdsAmount, which the header already holds; this
            // migration only moves the snapshot, so the model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
