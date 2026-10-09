import { Routes } from '@angular/router';

/** The site services routes (TK-169), mounted under the shell in `apps/web` for a Service branch with SiteServices add-on. */
export const siteRoutes: Routes = [
  {
    path: 'site',
    loadComponent: () => import('./sites/sites.page').then((m) => m.SitesPage),
    data: { access: { permission: 'site.view', businessTypes: ['Service'], addOns: ['SiteServices'] } },
  },
  {
    path: 'site/portal',
    loadComponent: () => import('./portal/laborer-portal.page').then((m) => m.LaborerPortalPage),
    data: { access: { permission: 'site.view', businessTypes: ['Service'], addOns: ['SiteServices'] } },
  },
];
