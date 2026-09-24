using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class SalesOrderInvoicedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InvoicedQuantity",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "chk_salesorderdetails_invoiced",
                schema: "sal",
                table: "SalesOrderDetails",
                sql: "\"InvoicedQuantity\" >= 0 AND \"InvoicedQuantity\" <= \"Quantity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_salesorderdetails_invoiced",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropColumn(
                name: "InvoicedQuantity",
                schema: "sal",
                table: "SalesOrderDetails");
        }
    }
}
