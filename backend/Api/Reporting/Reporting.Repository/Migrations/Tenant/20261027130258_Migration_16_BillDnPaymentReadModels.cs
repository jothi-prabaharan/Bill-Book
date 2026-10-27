using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class Migration_16_BillDnPaymentReadModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SpendMoneyRead gains TdsAmount and IsTdsChallan, which Accounting owns on acc.SpendMoney:
            // nothing in the database changes, only the model snapshot (TK-238).
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
