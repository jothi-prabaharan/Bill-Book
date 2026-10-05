import { Routes } from '@angular/router';

/** The work order page (S6, TK-66), mounted under the shell in `apps/web` for a School branch. */
export const workOrderRoutes: Routes = [
  {
    path: 'work-orders',
    loadComponent: () => import('./work-orders/work-orders.page').then((m) => m.WorkOrdersPage),
    data: { access: { permission: 'workorder.view', businessTypes: ['School'] } },
  },
];
