import { Routes } from '@angular/router';

/** The preventive plans page (S7, TK-67), mounted under the shell by `apps/school`. */
export const preventiveRoutes: Routes = [
  {
    path: 'preventive/plans',
    loadComponent: () => import('./plans/plans.page').then((m) => m.PlansPage),
    data: { access: { permission: 'preventive.view', apps: ['School'] } },
  },
];
