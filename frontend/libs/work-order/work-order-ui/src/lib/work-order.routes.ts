import { Routes } from '@angular/router';

/** The work order page (S6, TK-66), mounted under the shell by `apps/school`. */
export const workOrderRoutes: Routes = [
  {
    path: 'work-orders',
    loadComponent: () => import('./work-orders/work-orders.page').then((m) => m.WorkOrdersPage),
    data: { access: { permission: 'workorder.view', apps: ['School'] } },
  },
];
