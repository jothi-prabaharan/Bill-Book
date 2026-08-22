using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <summary>
    /// Ten shadow foreign keys, dropped — and with them the reason no sales
    /// document could ever be saved.
    ///
    /// Each header-to-line relationship was configured as
    /// <c>HasOne&lt;Quote&gt;().WithMany()</c>, which is a valid relationship on
    /// the real <c>QuoteId</c> column that has nothing to do with the
    /// <c>Quote.Lines</c> collection. EF therefore mapped that collection a
    /// second time by convention, inventing <c>QuoteId1</c> and nine more like
    /// it. Adding a line through the navigation — which is what every service
    /// does — filled the shadow column and left the real <c>NOT NULL</c> one at
    /// zero, so <c>SaveChanges</c> came back with a foreign key violation on
    /// every create, in all five document types.
    ///
    /// <b>Nothing is lost with them.</b> The columns were nullable and never
    /// written: a row that reached them would have had to be saved first, and
    /// that is precisely what could not happen. The scaffolder's data-loss
    /// warning is about the shape of the operation, not about this schema.
    ///
    /// The model side is <c>SalesDbContext.OnModelCreating</c>, which now names
    /// the collection on all ten; the six conversion links beside them stay
    /// navigation-less, deliberately.
    /// </summary>
    public partial class BindDocumentLineNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditNoteDetailTaxes_CreditNoteDetails_CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditNoteDetails_CreditNotes_CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryChallanDetailTaxes_DeliveryChallanDetails_Delivery~1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryChallanDetails_DeliveryChallans_DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetailTaxes_InvoiceDetails_InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteDetailTaxes_QuoteDetails_QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteDetails_Quotes_QuoteId1",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderDetailTaxes_SalesOrderDetails_SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderDetails_SalesOrders_SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetails_SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderDetailTaxes_SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetails_QuoteId1",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_QuoteDetailTaxes_QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetails_InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceDetailTaxes_InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetails_DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryChallanDetailTaxes_DeliveryChallanDetailId1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetails_CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_CreditNoteDetailTaxes_CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails");

            migrationBuilder.DropColumn(
                name: "SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes");

            migrationBuilder.DropColumn(
                name: "QuoteId1",
                schema: "sal",
                table: "QuoteDetails");

            migrationBuilder.DropColumn(
                name: "QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails");

            migrationBuilder.DropColumn(
                name: "InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes");

            migrationBuilder.DropColumn(
                name: "DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails");

            migrationBuilder.DropColumn(
                name: "DeliveryChallanDetailId1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails");

            migrationBuilder.DropColumn(
                name: "CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QuoteId1",
                schema: "sal",
                table: "QuoteDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeliveryChallanDetailId1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetails_SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails",
                column: "SalesOrderId1");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderDetailTaxes_SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                column: "SalesOrderDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetails_QuoteId1",
                schema: "sal",
                table: "QuoteDetails",
                column: "QuoteId1");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteDetailTaxes_QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes",
                column: "QuoteDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails",
                column: "InvoiceId1");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetailTaxes_InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                column: "InvoiceDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetails_DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "DeliveryChallanId1");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryChallanDetailTaxes_DeliveryChallanDetailId1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                column: "DeliveryChallanDetailId1");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetails_CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "CreditNoteId1");

            migrationBuilder.CreateIndex(
                name: "IX_CreditNoteDetailTaxes_CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                column: "CreditNoteDetailId1");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNoteDetailTaxes_CreditNoteDetails_CreditNoteDetailId1",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                column: "CreditNoteDetailId1",
                principalSchema: "sal",
                principalTable: "CreditNoteDetails",
                principalColumn: "CreditNoteDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditNoteDetails_CreditNotes_CreditNoteId1",
                schema: "sal",
                table: "CreditNoteDetails",
                column: "CreditNoteId1",
                principalSchema: "sal",
                principalTable: "CreditNotes",
                principalColumn: "CreditNoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryChallanDetailTaxes_DeliveryChallanDetails_Delivery~1",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                column: "DeliveryChallanDetailId1",
                principalSchema: "sal",
                principalTable: "DeliveryChallanDetails",
                principalColumn: "DeliveryChallanDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryChallanDetails_DeliveryChallans_DeliveryChallanId1",
                schema: "sal",
                table: "DeliveryChallanDetails",
                column: "DeliveryChallanId1",
                principalSchema: "sal",
                principalTable: "DeliveryChallans",
                principalColumn: "DeliveryChallanId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetailTaxes_InvoiceDetails_InvoiceDetailId1",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                column: "InvoiceDetailId1",
                principalSchema: "sal",
                principalTable: "InvoiceDetails",
                principalColumn: "InvoiceDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceDetails_Invoices_InvoiceId1",
                schema: "sal",
                table: "InvoiceDetails",
                column: "InvoiceId1",
                principalSchema: "sal",
                principalTable: "Invoices",
                principalColumn: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteDetailTaxes_QuoteDetails_QuoteDetailId1",
                schema: "sal",
                table: "QuoteDetailTaxes",
                column: "QuoteDetailId1",
                principalSchema: "sal",
                principalTable: "QuoteDetails",
                principalColumn: "QuoteDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteDetails_Quotes_QuoteId1",
                schema: "sal",
                table: "QuoteDetails",
                column: "QuoteId1",
                principalSchema: "sal",
                principalTable: "Quotes",
                principalColumn: "QuoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderDetailTaxes_SalesOrderDetails_SalesOrderDetailId1",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                column: "SalesOrderDetailId1",
                principalSchema: "sal",
                principalTable: "SalesOrderDetails",
                principalColumn: "SalesOrderDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderDetails_SalesOrders_SalesOrderId1",
                schema: "sal",
                table: "SalesOrderDetails",
                column: "SalesOrderId1",
                principalSchema: "sal",
                principalTable: "SalesOrders",
                principalColumn: "SalesOrderId");
        }
    }
}
