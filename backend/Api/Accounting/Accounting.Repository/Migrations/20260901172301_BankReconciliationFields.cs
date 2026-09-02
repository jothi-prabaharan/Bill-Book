using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class BankReconciliationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BankStatementLineId",
                schema: "acc",
                table: "JournalLedger",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReconciled",
                schema: "acc",
                table: "JournalLedger",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "acc",
                table: "BankStatementLines",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsReconciled",
                schema: "acc",
                table: "BankStatementLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankStatementLineId",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropColumn(
                name: "IsReconciled",
                schema: "acc",
                table: "JournalLedger");

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "acc",
                table: "BankStatementLines");

            migrationBuilder.DropColumn(
                name: "IsReconciled",
                schema: "acc",
                table: "BankStatementLines");
        }
    }
}
