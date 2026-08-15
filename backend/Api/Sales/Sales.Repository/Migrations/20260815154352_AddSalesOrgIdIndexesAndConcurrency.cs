using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrgIdIndexesAndConcurrency : Migration
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

            // sal.SalesRegister was left out of the RLS loop when the schema was
            // written: fifteen of the sixteen tables carry a policy and this one
            // does not. It is an OrgScopedEntity like the rest, and it holds the
            // figures GSTR-1 is filed from — so it was the one table in the
            // schema with neither of the two guards, the query filter being
            // absent everywhere at the time.
            migrationBuilder.Sql("ALTER TABLE sal.\"SalesRegister\" ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS salesregister_org_isolation ON sal.\"SalesRegister\";");
            migrationBuilder.Sql(
                "CREATE POLICY salesregister_org_isolation ON sal.\"SalesRegister\" " +
                "USING (\"OrgId\" = current_setting('app.current_org_id', true)::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP POLICY IF EXISTS salesregister_org_isolation ON sal.\"SalesRegister\";");

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

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "SalesRegister",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "SalesOrders",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "SalesOrderDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "Quotes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "QuoteDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "Invoices",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "InvoiceDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallans",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "DeliveryChallanDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "CreditNotes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetails",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "Version",
                schema: "sal",
                table: "CreditNoteDetailTaxes",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

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
