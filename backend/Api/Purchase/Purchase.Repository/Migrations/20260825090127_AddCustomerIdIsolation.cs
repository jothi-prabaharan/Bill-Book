using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Purchase.Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_OrgId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderDetails_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderDetailTaxes_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_OrgId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptDetails_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetails");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptDetailTaxes_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_DebitNotes_OrgId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropIndex(
                name: "IX_DebitNoteDetails_OrgId",
                schema: "pur",
                table: "DebitNoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_DebitNoteDetailTaxes_OrgId",
                schema: "pur",
                table: "DebitNoteDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Bills_OrgId",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillDetails_OrgId",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropIndex(
                name: "IX_BillDetailTaxes_OrgId",
                schema: "pur",
                table: "BillDetailTaxes");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrderDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceipts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNotes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNoteDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNoteDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "Bills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "BillDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pur",
                table: "BillDetailTaxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrders",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetails_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceipts",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNotes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId_OrgId",
                schema: "pur",
                table: "Bills",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetails",
                columns: new[] { "CustomerId", "OrgId" });

            migrationBuilder.CreateIndex(
                name: "IX_BillDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetailTaxes",
                columns: new[] { "CustomerId", "OrgId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderDetails_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptDetails_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetails");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceiptDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_DebitNotes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropIndex(
                name: "IX_DebitNoteDetails_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetails");

            migrationBuilder.DropIndex(
                name: "IX_DebitNoteDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "DebitNoteDetailTaxes");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CustomerId_OrgId",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillDetails_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropIndex(
                name: "IX_BillDetailTaxes_CustomerId_OrgId",
                schema: "pur",
                table: "BillDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrderDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceiptDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNotes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNoteDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "DebitNoteDetailTaxes");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "BillDetails");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pur",
                table: "BillDetailTaxes");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrgId",
                schema: "pur",
                table: "PurchaseOrders",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetails_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderDetailTaxes_OrgId",
                schema: "pur",
                table: "PurchaseOrderDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_OrgId",
                schema: "pur",
                table: "GoodsReceipts",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetails_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceiptDetailTaxes_OrgId",
                schema: "pur",
                table: "GoodsReceiptDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNotes_OrgId",
                schema: "pur",
                table: "DebitNotes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetails_OrgId",
                schema: "pur",
                table: "DebitNoteDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_DebitNoteDetailTaxes_OrgId",
                schema: "pur",
                table: "DebitNoteDetailTaxes",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OrgId",
                schema: "pur",
                table: "Bills",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BillDetails_OrgId",
                schema: "pur",
                table: "BillDetails",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_BillDetailTaxes_OrgId",
                schema: "pur",
                table: "BillDetailTaxes",
                column: "OrgId");
        }
    }
}
