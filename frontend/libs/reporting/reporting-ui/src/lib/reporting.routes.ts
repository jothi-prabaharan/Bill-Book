import { Routes } from '@angular/router';

/**
 * The reports routes: a catalog, and one host page for every report in it.
 *
 * `:reportKey` is what makes forty-five reports cost two pages. The permission on
 * both is `reports.view`; the report's own module permission is checked by the
 * server, which is the only place it can be checked honestly.
 */
export const reportingRoutes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./report-list/report-list.page').then((m) => m.ReportListPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'profit-and-loss',
    loadComponent: () =>
      import('./profit-and-loss/profit-and-loss-report.page').then((m) => m.ProfitAndLossReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-201), at the path the menu links to.
    path: 'cash-flow-statement-direct',
    loadComponent: () =>
      import('./cash-flow/cash-flow-report.page').then((m) => m.CashFlowReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-202), at the path the menu links to.
    path: 'reconciliation-report',
    loadComponent: () =>
      import('./bank-reconciliation/bank-reconciliation-report.page').then((m) => m.BankReconciliationReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-203), ahead of the shared host.
    path: 'business-performance',
    loadComponent: () =>
      import('./business-performance/business-performance-report.page').then((m) => m.BusinessPerformanceReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoints (TK-204), ahead of the shared host.
    // The server checks the same add-on.
    path: 'budget-vs-actual',
    loadComponent: () =>
      import('./budget-vs-actual/budget-vs-actual-report.page').then((m) => m.BudgetVsActualReportPage),
    data: { access: { permission: 'reports.view', addOns: ['Budget'] } },
  },
  {
    // Its own page on its own endpoints (TK-207), ahead of the shared host.
    // The server checks the same add-on.
    path: 'budget-vs-actual-by-project',
    loadComponent: () =>
      import('./budget-vs-actual-by-project/budget-vs-actual-by-project-report.page').then(
        (m) => m.BudgetVsActualByProjectReportPage,
      ),
    data: { access: { permission: 'reports.view', addOns: ['Budget'] } },
  },
  {
    // Its own page on its own endpoint (TK-214), ahead of the shared host.
    // The server checks the same add-on.
    path: 'budget-monthly-trend',
    loadComponent: () =>
      import('./budget-monthly-trend/budget-monthly-trend-report.page').then((m) => m.BudgetMonthlyTrendReportPage),
    data: { access: { permission: 'reports.view', addOns: ['Budget'] } },
  },
  {
    // Its own page on its own endpoint (TK-217), ahead of the shared host.
    path: 'journal-report',
    loadComponent: () => import('./journal-report/journal-report.page').then((m) => m.JournalReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-218), ahead of the shared host.
    path: 'account-transaction',
    loadComponent: () =>
      import('./account-transaction/account-transaction.page').then((m) => m.AccountTransactionPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-219), ahead of the shared host.
    path: 'bank-summary',
    loadComponent: () => import('./bank-summary/bank-summary.page').then((m) => m.BankSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-239), at the path the menu links to.
    path: 'purchase-analysis',
    loadComponent: () => import('./purchase-analysis/purchase-analysis.page').then((m) => m.PurchaseAnalysisPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-238), at the path the menu links to.
    path: 'bill-dn-payment-report',
    loadComponent: () => import('./bill-dn-payment/bill-dn-payment.page').then((m) => m.BillDnPaymentPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-237), at the path the menu links to.
    path: 'bills-track-report',
    loadComponent: () => import('./bills-track/bills-track.page').then((m) => m.BillsTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-236), at the path the menu links to.
    path: 'payable-invoice-detail',
    loadComponent: () => import('./payable-invoice-detail/payable-invoice-detail.page').then((m) => m.PayableInvoiceDetailPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-235), at the path the menu links to.
    path: 'payable-invoice-summary',
    loadComponent: () => import('./payable-invoice-summary/payable-invoice-summary.page').then((m) => m.PayableInvoiceSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-234), at the path the menu links to.
    path: 'aged-payables-details',
    loadComponent: () => import('./aged-payables-details/aged-payables-details.page').then((m) => m.AgedPayablesDetailsPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-233), at the path the menu links to.
    path: 'aged-payables-summary',
    loadComponent: () => import('./aged-payables-summary/aged-payables-summary.page').then((m) => m.AgedPayablesSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-232), at the path the menu links to.
    path: 'delivery-order-track-report',
    loadComponent: () => import('./delivery-order-track/delivery-order-track.page').then((m) => m.DeliveryOrderTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-231), at the path the menu links to.
    path: 'quotation-track-report',
    loadComponent: () => import('./quotation-track/quotation-track.page').then((m) => m.QuotationTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-243), at the path the menu links to.
    path: 'inventory-item-summary',
    loadComponent: () => import('./inventory-item-summary/inventory-item-summary.page').then((m) => m.InventoryItemSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-245), at the path the menu links to.
    path: 'inventory-item-list',
    loadComponent: () => import('./inventory-item-list/inventory-item-list-report.page').then((m) => m.InventoryItemListPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'inventory-aging-report',
    loadComponent: () => import('./inventory-aging/inventory-aging-report.page').then((m) => m.InventoryAgingReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'inventory-aging',
    redirectTo: 'inventory-aging-report',
    pathMatch: 'full',
  },
  {
    path: 'batch-tracking-status-report',
    loadComponent: () =>
      import('./batch-tracking-status/batch-tracking-status-report.page').then((m) => m.BatchTrackingStatusReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'batch-tracking-status',
    redirectTo: 'batch-tracking-status-report',
    pathMatch: 'full',
  },
  {
    path: 'batch-tracking-detail-report',
    loadComponent: () =>
      import('./batch-tracking-detail/batch-tracking-detail-report.page').then((m) => m.BatchTrackingDetailReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'batch-tracking-detail',
    redirectTo: 'batch-tracking-detail-report',
    pathMatch: 'full',
  },
  {
    path: 'serial-tracking-status-report',
    loadComponent: () =>
      import('./serial-tracking-status/serial-tracking-status-report.page').then((m) => m.SerialTrackingStatusReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'serial-tracking-status',
    redirectTo: 'serial-tracking-status-report',
    pathMatch: 'full',
  },
  {
    path: 'serial-tracking-detail-report',
    loadComponent: () =>
      import('./serial-tracking-detail/serial-tracking-detail-report.page').then((m) => m.SerialTrackingDetailReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'serial-tracking-detail',
    redirectTo: 'serial-tracking-detail-report',
    pathMatch: 'full',
  },
  {
    path: 'warehouse-tracking-status-report',
    loadComponent: () =>
      import('./warehouse-tracking-status/warehouse-tracking-status-report.page').then((m) => m.WarehouseTrackingStatusReportPageComponent),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'warehouse-tracking-status',
    redirectTo: 'warehouse-tracking-status-report',
    pathMatch: 'full',
  },
  {
    path: 'fixed-assets-schedule',
    loadComponent: () =>
      import('./fixed-assets-schedule/fixed-assets-schedule-report.page').then((m) => m.FixedAssetsScheduleReportPageComponent),
    data: { access: { permission: 'reports.view', addOns: ['FixedAssets'] } },
  },
  {
    path: 'depreciation-schedule',
    loadComponent: () =>
      import('./depreciation-schedule/depreciation-schedule-report.page').then((m) => m.DepreciationScheduleReportPageComponent),
    data: { access: { permission: 'reports.view', addOns: ['FixedAssets'] } },
  },
  {
    // The menu links to /reports/disposal-schedule-report (TK-254)
    path: 'disposal-schedule-report',
    loadComponent: () =>
      import('./disposal-schedule/disposal-schedule-report.page').then((m) => m.DisposalScheduleReportPageComponent),
    data: { access: { permission: 'reports.view', addOns: ['FixedAssets'] } },
  },
  {
    path: 'disposal-schedule',
    redirectTo: 'disposal-schedule-report',
    pathMatch: 'full',
  },
  {
    path: 'fixed-asset-reconciliation',
    loadComponent: () =>
      import('./fixed-asset-reconciliation/fixed-asset-reconciliation-report.page').then((m) => m.FixedAssetReconciliationReportPageComponent),
    data: { access: { permission: 'reports.view', addOns: ['FixedAssets'] } },
  },
  {
    // Its own page on its own endpoint (TK-244), at the path the menu links to.
    path: 'inventory-item-detail',
    loadComponent: () => import('./inventory-item-detail/inventory-item-detail-report.page').then((m) => m.InventoryItemDetailPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-242), at the path the menu links to.
    path: 'receive-order-track-report',
    loadComponent: () => import('./receive-order-track/receive-order-track.page').then((m) => m.ReceiveOrderTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-241), at the path the menu links to.
    path: 'purchase-receive-order-details',
    loadComponent: () => import('./purchase-receive-order-details/purchase-receive-order-details.page').then((m) => m.PurchaseReceiveOrderDetailsPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-240), at the path the menu links to.
    path: 'purchase-order-track-report',
    loadComponent: () => import('./purchase-order-track/purchase-order-track.page').then((m) => m.PurchaseOrderTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-230), at the path the menu links to.
    path: 'sales-order-track-report',
    loadComponent: () => import('./sales-order-track/sales-order-track.page').then((m) => m.SalesOrderTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-229), at the path the menu links to.
    path: 'sales-analysis-detail-report',
    loadComponent: () => import('./sales-analysis-detail/sales-analysis-detail.page').then((m) => m.SalesAnalysisDetailPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-228), at the path the menu links to.
    path: 'sales-analysis',
    loadComponent: () => import('./sales-analysis/sales-analysis.page').then((m) => m.SalesAnalysisPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-227), at the path the menu links to.
    path: 'invoice-dn-payment-collection-report',
    loadComponent: () =>
      import('./invoice-dn-payment-collection/invoice-dn-payment-collection.page').then((m) => m.InvoiceDnPaymentCollectionPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-226), at the path the menu links to.
    path: 'invoice-track-report',
    loadComponent: () => import('./invoice-track/invoice-track.page').then((m) => m.InvoiceTrackPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-225), at the path the menu links to.
    path: 'receivable-invoice-detail',
    loadComponent: () =>
      import('./receivable-invoice-detail/receivable-invoice-detail.page').then((m) => m.ReceivableInvoiceDetailPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-224), at the path the menu links to.
    path: 'receivable-invoice-summary',
    loadComponent: () =>
      import('./receivable-invoice-summary/receivable-invoice-summary.page').then((m) => m.ReceivableInvoiceSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-223), at the path the menu links to.
    path: 'aged-receivables-detail',
    loadComponent: () =>
      import('./aged-receivables-detail/aged-receivables-detail.page').then((m) => m.AgedReceivablesDetailPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-222), at the path the menu links to.
    path: 'aged-receivables-summary',
    loadComponent: () =>
      import('./aged-receivables-summary/aged-receivables-summary.page').then((m) => m.AgedReceivablesSummaryPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-221), ahead of the shared host.
    path: 'foreign-currency-gain-or-loss-details',
    loadComponent: () =>
      import('./foreign-currency-gain-or-loss-details/foreign-currency-gain-or-loss-details.page').then((m) => m.ForeignCurrencyGainLossDetailsPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-220), ahead of the shared host.
    path: 'foreign-currency-gain-or-loss',
    loadComponent: () =>
      import('./foreign-currency-gain-or-loss/foreign-currency-gain-or-loss.page').then((m) => m.ForeignCurrencyGainLossPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    path: 'balance-sheet',
    loadComponent: () =>
      import('./balance-sheet/balance-sheet-report.page').then((m) => m.BalanceSheetReportPage),
    data: { access: { permission: 'reports.view' } },
  },
  {
    // Its own page on its own endpoint (TK-198), ahead of the shared host.
    // The server checks the same add-on and permission.
    path: 'salary-increment',
    loadComponent: () =>
      import('./salary-increment/salary-increment-report.page').then((m) => m.SalaryIncrementReportPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: ':reportKey',
    loadComponent: () =>
      import('./report-host/report-host.page').then((m) => m.ReportHostPage),
    data: { access: { permission: 'reports.view' } },
  },
];
