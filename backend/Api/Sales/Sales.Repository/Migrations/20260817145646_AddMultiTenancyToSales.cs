using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <summary>
    /// Brings the sal schema onto TenantDbContext: an OrgId index per table, and
    /// the concurrency token moved to the PostgreSQL system column.
    ///
    /// As scaffolded this migration renamed Version to xmin and then altered it to
    /// xid. Both are impossible. xmin is a system column that exists on every
    /// table already, so Postgres rejects the rename with 42701, "column name
    /// xmin conflicts with a system column name", and Sales.Api died on startup
    /// before it could listen.
    ///
    /// Npgsql silently omits xmin from CreateTable, which is why Accounting and
    /// Inventory scaffolded correctly — they never had a real Version column. The
    /// sal tables were created before Sales moved onto TenantDbContext, so they do
    /// carry one, and the right operation is to drop it. The model maps Version to
    /// the system xmin, which is read and never written.
    /// </summary>
    public partial class AddMultiTenancyToSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "SalesRegister");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetailTaxes");

            migrationBuilder.CreateIndex(
                name: "IX_SalesRegister_OrgId",
                schema: "sal",
                table: "SalesRegister",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_OrgId",
                schema: "sal",
                table: "SalesOrders",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_OrgId",
                schema: "sal",
                table: "SalesOrderDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_OrgId",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_OrgId",
                schema: "sal",
                table: "Quotes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_OrgId",
                schema: "sal",
                table: "QuoteDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_OrgId",
                schema: "sal",
                table: "QuoteDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrgId",
                schema: "sal",
                table: "Invoices",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_OrgId",
                schema: "sal",
                table: "InvoiceDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_OrgId",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_OrgId",
                schema: "sal",
                table: "DeliveryChallans",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_OrgId",
                schema: "sal",
                table: "CreditNotes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_OrgId",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_OrgId",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                column: "OrgId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesRegister_OrgId",
                schema: "sal",
                table: "SalesRegister");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_OrgId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetails_OrgId",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetailTaxes_OrgId",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_OrgId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetails_OrgId",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetailTaxes_OrgId",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrgId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_OrgId",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetailTaxes_OrgId",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallans_OrgId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetails_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetailTaxes_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_OrgId",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetails_OrgId",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetailTaxes_OrgId",
                schema: "sal",
                table: "CreditNoteDetailTaxes");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesRegister",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "Quotes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "Invoices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNotes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
