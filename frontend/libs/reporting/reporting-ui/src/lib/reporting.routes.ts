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
    // Its own page on its own endpoint (TK-231), at the path the menu links to.
    path: 'quotation-track-report',
    loadComponent: () => import('./quotation-track/quotation-track.page').then((m) => m.QuotationTrackPage),
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
