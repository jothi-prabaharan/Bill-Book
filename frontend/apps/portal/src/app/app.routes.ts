import { Route } from '@angular/router';

export const appRoutes: Route[] = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('@bill-book/auth').then(m => m.LoginPage)
  },
  {
    path: 'dashboard',
    loadComponent: () => import('./portal-dashboard/portal-dashboard.page').then(m => m.PortalDashboardPage)
  },
  {
    path: 'statement',
    loadComponent: () => import('./portal-statement-list/portal-statement.list').then(m => m.PortalStatementList)
  }
];

