using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBankParentAccountsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // What makes bank-account provisioning idempotent: Banking asks for
            // the account behind bank account 7, Accounting keys it on
            // "BANK:7", and a retried call after a dropped response finds the
            // existing row rather than creating a second account.
            //
            // It also enforces that the seeded control-account names stay unique
            // within an organization, which nothing guaranteed before.
            migrationBuilder.CreateIndex(
                name: "IX_Accounts_SystemName",
                schema: "acc",
                table: "Accounts",
                columns: new[] { "OrgId", "AccountSystemName" },
                unique: true,
                filter: "\"AccountSystemName\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_SystemName",
                schema: "acc",
                table: "Accounts");
        }
    }
}
