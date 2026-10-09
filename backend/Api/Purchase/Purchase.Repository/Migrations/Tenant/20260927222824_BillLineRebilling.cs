using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class BillLineRebilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BilledInvoiceId",
                schema: "pur",
                table: "BillDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBillable",
                schema: "pur",
                table: "BillDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkupPercent",
                schema: "pur",
                table: "BillDetails",
                type: "numeric(7,2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_OrgId_BilledInvoiceId",
                schema: "pur",
                table: "BillDetails",
                columns: new[] { "OrgId", "BilledInvoiceId" },
                filter: "\"BilledInvoiceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillDetails_OrgId_BilledInvoiceId",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropColumn(
                name: "BilledInvoiceId",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropColumn(
                name: "IsBillable",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropColumn(
                name: "MarkupPercent",
                schema: "pur",
                table: "BillDetails");
        }
    }
}
