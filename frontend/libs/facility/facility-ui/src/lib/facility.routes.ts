import { Routes } from '@angular/router';

/** The Facility pages (S5, TK-65), mounted under the shell by `apps/school`. */
export const facilityRoutes: Routes = [
  {
    path: 'facility/spaces',
    loadComponent: () => import('./spaces/spaces.page').then((m) => m.SpacesPage),
    data: { access: { permission: 'facility.view', apps: ['School'] } },
  },
  {
    path: 'facility/assets',
    loadComponent: () => import('./assets/assets.page').then((m) => m.AssetsPage),
    data: { access: { permission: 'facility.view', apps: ['School'] } },
  },
];
