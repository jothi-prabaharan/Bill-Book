using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class LineProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "sal",
                table: "QuoteDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "sal",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProjectId",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                schema: "sal",
                table: "CreditNoteDetails");
        }
    }
}
