import { Routes } from '@angular/router';

/** The Fee pages (S4, TK-64), mounted under the shell in `apps/web` for a School branch. */
export const feeRoutes: Routes = [
  {
    path: 'fee/setup',
    loadComponent: () => import('./setup/fee-setup.page').then((m) => m.FeeSetupPage),
    data: { access: { permission: 'fee.view', businessTypes: ['School'] } },
  },
  {
    path: 'fee/demands',
    loadComponent: () => import('./demands/demands.page').then((m) => m.DemandsPage),
    data: { access: { permission: 'fee.view', businessTypes: ['School'] } },
  },
  {
    path: 'fee/receipts',
    loadComponent: () => import('./receipts/receipts.page').then((m) => m.ReceiptsPage),
    data: { access: { permission: 'fee.view', businessTypes: ['School'] } },
  },
];
