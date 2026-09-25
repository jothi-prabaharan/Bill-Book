import { Routes } from '@angular/router';

/** The AMC contracts page (S8, TK-68), mounted under the shell by `apps/school`. */
export const maintenanceContractRoutes: Routes = [
  {
    path: 'amc/contracts',
    loadComponent: () => import('./contracts/contracts.page').then((m) => m.ContractsPage),
    data: { access: { permission: 'amc.view', apps: ['School'] } },
  },
];
