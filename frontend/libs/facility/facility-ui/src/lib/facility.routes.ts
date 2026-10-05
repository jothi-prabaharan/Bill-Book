import { Routes } from '@angular/router';

/** The Facility pages (S5, TK-65), mounted under the shell in `apps/web` for a School branch. */
export const facilityRoutes: Routes = [
  {
    path: 'facility/spaces',
    loadComponent: () => import('./spaces/spaces.page').then((m) => m.SpacesPage),
    data: { access: { permission: 'facility.view', businessTypes: ['School'] } },
  },
  {
    path: 'facility/assets',
    loadComponent: () => import('./assets/assets.page').then((m) => m.AssetsPage),
    data: { access: { permission: 'facility.view', businessTypes: ['School'] } },
  },
];
