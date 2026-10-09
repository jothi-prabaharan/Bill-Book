using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Master.Repository.Migrations.Admin
{
    /// <inheritdoc />
    public partial class UpdateReportsMenuRoute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "RoutePath",
                value: "/reports/balance-sheet");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "RoutePath",
                value: "/reports/profit-and-loss");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "RoutePath",
                value: "/reports/cash-flow-statement-direct");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "RoutePath",
                value: "/reports/reconciliation-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "RoutePath",
                value: "/reports/business-performance");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "RoutePath",
                value: "/reports/journal-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "RoutePath",
                value: "/reports/account-transaction");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "RoutePath",
                value: "/reports/bank-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "RoutePath",
                value: "/reports/foreign-currency-gain-or-loss");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "RoutePath",
                value: "/reports/foreign-currency-gain-or-loss-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "RoutePath",
                value: "/reports/aged-receivables-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "RoutePath",
                value: "/reports/aged-receivables-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "RoutePath",
                value: "/reports/receivable-invoice-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "RoutePath",
                value: "/reports/receivable-invoice-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "RoutePath",
                value: "/reports/invoice-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "RoutePath",
                value: "/reports/invoice-dn-payment-collection-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "RoutePath",
                value: "/reports/sales-analysis");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "RoutePath",
                value: "/reports/sales-analysis-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "RoutePath",
                value: "/reports/sales-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "RoutePath",
                value: "/reports/quotation-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "RoutePath",
                value: "/reports/delivery-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "RoutePath",
                value: "/reports/aged-payables-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "RoutePath",
                value: "/reports/aged-payables-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "RoutePath",
                value: "/reports/payable-invoice-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "RoutePath",
                value: "/reports/payable-invoice-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "RoutePath",
                value: "/reports/bills-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "RoutePath",
                value: "/reports/bill-dn-payment-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "RoutePath",
                value: "/reports/purchase-analysis");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "RoutePath",
                value: "/reports/purchase-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "RoutePath",
                value: "/reports/purchase-receive-order-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "RoutePath",
                value: "/reports/receive-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "RoutePath",
                value: "/reports/inventory-item-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "RoutePath",
                value: "/reports/inventory-item-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "RoutePath",
                value: "/reports/inventory-item-list");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "RoutePath",
                value: "/reports/inventory-aging-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "RoutePath",
                value: "/reports/batch-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "RoutePath",
                value: "/reports/batch-tracking-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "RoutePath",
                value: "/reports/serial-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "RoutePath",
                value: "/reports/serial-tracking-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "RoutePath",
                value: "/reports/warehouse-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "RoutePath",
                value: "/reports/fixed-assets-schedule");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "RoutePath",
                value: "/reports/depreciation-schedule");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "RoutePath",
                value: "/reports/disposal-schedule-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "RoutePath",
                value: "/reports/fixed-asset-reconciliation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1033,
                column: "RoutePath",
                value: "/reports/statements/balance-sheet");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1034,
                column: "RoutePath",
                value: "/reports/statements/profit-and-loss");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1035,
                column: "RoutePath",
                value: "/reports/r-cash-flow-statement-direct");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1036,
                column: "RoutePath",
                value: "/reports/r-reconciliation-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1037,
                column: "RoutePath",
                value: "/reports/r-business-performance");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1040,
                column: "RoutePath",
                value: "/reports/r-journal-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1041,
                column: "RoutePath",
                value: "/reports/r-account-transaction");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1042,
                column: "RoutePath",
                value: "/reports/r-bank-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1043,
                column: "RoutePath",
                value: "/reports/r-foreign-currency-gain-or-loss");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1044,
                column: "RoutePath",
                value: "/reports/r-foreign-currency-gain-or-loss-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1045,
                column: "RoutePath",
                value: "/reports/r-aged-receivables-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1046,
                column: "RoutePath",
                value: "/reports/r-aged-receivables-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1047,
                column: "RoutePath",
                value: "/reports/r-receivable-invoice-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1048,
                column: "RoutePath",
                value: "/reports/r-receivable-invoice-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1049,
                column: "RoutePath",
                value: "/reports/r-invoice-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1050,
                column: "RoutePath",
                value: "/reports/r-invoice-dn-payment-collection-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1051,
                column: "RoutePath",
                value: "/reports/r-sales-analysis");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1052,
                column: "RoutePath",
                value: "/reports/r-sales-analysis-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1053,
                column: "RoutePath",
                value: "/reports/r-sales-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1054,
                column: "RoutePath",
                value: "/reports/r-quotation-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1055,
                column: "RoutePath",
                value: "/reports/r-delivery-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1056,
                column: "RoutePath",
                value: "/reports/r-aged-payables-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1057,
                column: "RoutePath",
                value: "/reports/r-aged-payables-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1058,
                column: "RoutePath",
                value: "/reports/r-payable-invoice-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1059,
                column: "RoutePath",
                value: "/reports/r-payable-invoice-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1060,
                column: "RoutePath",
                value: "/reports/r-bills-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1061,
                column: "RoutePath",
                value: "/reports/r-bill-dn-payment-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1062,
                column: "RoutePath",
                value: "/reports/r-purchase-analysis");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1063,
                column: "RoutePath",
                value: "/reports/r-purchase-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1064,
                column: "RoutePath",
                value: "/reports/r-purchase-receive-order-details");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1065,
                column: "RoutePath",
                value: "/reports/r-receive-order-track-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1066,
                column: "RoutePath",
                value: "/reports/r-inventory-item-summary");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1067,
                column: "RoutePath",
                value: "/reports/r-inventory-item-detail");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1068,
                column: "RoutePath",
                value: "/reports/r-inventory-item-list");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1069,
                column: "RoutePath",
                value: "/reports/r-inventory-aging-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1070,
                column: "RoutePath",
                value: "/reports/r-batch-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1071,
                column: "RoutePath",
                value: "/reports/r-batch-tracking-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1072,
                column: "RoutePath",
                value: "/reports/r-serial-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1073,
                column: "RoutePath",
                value: "/reports/r-serial-tracking-detail-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1074,
                column: "RoutePath",
                value: "/reports/r-warehouse-tracking-status-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1075,
                column: "RoutePath",
                value: "/reports/r-fixed-assets-schedule");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1076,
                column: "RoutePath",
                value: "/reports/r-depreciation-schedule");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1077,
                column: "RoutePath",
                value: "/reports/r-disposal-schedule-report");

            migrationBuilder.UpdateData(
                schema: "mst",
                table: "Menus",
                keyColumn: "MenuId",
                keyValue: 1078,
                column: "RoutePath",
                value: "/reports/r-fixed-asset-reconciliation");
        }
    }
}
