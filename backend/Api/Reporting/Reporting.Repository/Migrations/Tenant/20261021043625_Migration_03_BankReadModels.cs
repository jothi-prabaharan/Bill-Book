using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_03_BankReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to run (TK-202): acc.TransferMoney is mapped as a read model,
            // and the money documents', statement lines' and bank accounts' statuses
            // are now read by name as Accounting stores them. All are Accounting's
            // tables, excluded from migrations; this migration only moves the
            // snapshot, so the model and it agree.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
