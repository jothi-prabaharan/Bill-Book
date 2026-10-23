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
