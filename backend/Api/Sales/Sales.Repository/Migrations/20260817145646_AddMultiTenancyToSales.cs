using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancyToSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "SalesRegister",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrders",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetails",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "Quotes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "QuoteDetails",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "QuoteDetailTaxes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "Invoices",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetails",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallans",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetails",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNotes",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetails",
                newName: "xmin");

            migrationBuilder.RenameColumn(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                newName: "xmin");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "SalesRegister",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "SalesOrders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "Quotes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "QuoteDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "Invoices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "InvoiceDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallans",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "CreditNotes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<uint>(
                name: "xmin",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

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

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "SalesRegister",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "SalesOrders",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "SalesOrderDetails",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "Quotes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "QuoteDetails",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "QuoteDetailTaxes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "Invoices",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "InvoiceDetails",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallans",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallanDetails",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "CreditNotes",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "CreditNoteDetails",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "xmin",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                newName: "Version");

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesRegister",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrders",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "Quotes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "Invoices",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallans",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNotes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);

            migrationBuilder.AlterColumn<long>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(uint),
                oldType: "xid",
                oldRowVersion: true);
        }
    }
}
