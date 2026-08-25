using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "SalesRegister",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "Quotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "QuoteDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "InvoiceDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "CreditNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_SalesRegister_CustomerId_OrgId",
                schema: "sal",
                table: "SalesRegister",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId_OrgId",
                schema: "sal",
                table: "Quotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId_OrgId",
                schema: "sal",
                table: "Invoices",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallans_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallans",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNotes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesRegister_CustomerId_OrgId",
                schema: "sal",
                table: "SalesRegister");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetails_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_CustomerId_OrgId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId_OrgId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallans_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetails_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNotes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetails_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetailTaxes_CustomerId_OrgId",
                schema: "sal",
                table: "CreditNoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "SalesRegister");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallans");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "CreditNotes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
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
    }
}
