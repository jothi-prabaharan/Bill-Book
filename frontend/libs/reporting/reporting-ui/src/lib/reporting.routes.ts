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
      import('./statements/profit-and-loss.page').then((m) => m.ProfitAndLossPage),
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
