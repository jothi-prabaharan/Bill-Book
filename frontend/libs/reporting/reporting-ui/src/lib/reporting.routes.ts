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
    loadComponent: () =>
      import('./report-list/report-list.page').then((m) => m.ReportListPage),
    data: { permission: 'reports.view' },
  },
  {
    path: 'statements/profit-and-loss',
    loadComponent: () =>
      import('./statements/profit-and-loss.page').then((m) => m.ProfitAndLossPage),
    data: { permission: 'reports.view' },
  },
  {
    path: 'statements/balance-sheet',
    loadComponent: () =>
      import('./statements/balance-sheet.page').then((m) => m.BalanceSheetPage),
    data: { permission: 'reports.view' },
  },
  {
    path: ':reportKey',
    loadComponent: () =>
      import('./report-host/report-host.page').then((m) => m.ReportHostPage),
    data: { permission: 'reports.view' },
  },
];
