using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Accounting.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "acc",
                table: "TransferMoney",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "acc",
                table: "SpendMoney",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "acc",
                table: "ReceiveMoney",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "acc",
                table: "OpeningBalances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "acc",
                table: "Journals",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "acc",
                table: "TransferMoney");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "acc",
                table: "SpendMoney");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "acc",
                table: "ReceiveMoney");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "acc",
                table: "OpeningBalances");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "acc",
                table: "Journals");
        }
    }
}
