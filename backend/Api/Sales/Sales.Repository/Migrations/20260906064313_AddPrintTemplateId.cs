using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPrintTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "sal",
                table: "SalesOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "sal",
                table: "Quotes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "sal",
                table: "Invoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "sal",
                table: "DeliveryChallans",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PrintTemplateId",
                schema: "sal",
                table: "CreditNotes",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "PrintTemplateId",
                schema: "sal",
                table: "CreditNotes");
        }
    }
}
