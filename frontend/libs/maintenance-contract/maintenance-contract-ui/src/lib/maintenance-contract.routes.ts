import { Routes } from '@angular/router';

/** The AMC contracts page (S8, TK-68), mounted under the shell in `apps/web` for a School branch. */
export const maintenanceContractRoutes: Routes = [
  {
    path: 'amc/contracts',
    loadComponent: () => import('./contracts/contracts.page').then((m) => m.ContractsPage),
    data: { access: { permission: 'amc.view', businessTypes: ['School'] } },
  },
];
