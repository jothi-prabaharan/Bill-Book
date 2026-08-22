using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalLedgerIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_JournalLedger_OrgId_SubAccountId_LedgerDate",
                schema: "acc",
                table: "JournalLedger",
                columns: new[] { "OrgId", "SubAccountId", "LedgerDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalLedger_OrgId_SubAccountId_LedgerDate",
                schema: "acc",
                table: "JournalLedger");
        }
    }
}
