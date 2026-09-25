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
    // Where a portal link lands: keeps its token, then opens the right portal (TK-69).
    path: 'portal',
    loadComponent: () => import('./portal-access.page').then(m => m.PortalAccessPage)
  },
  {
    path: 'school',
    loadComponent: () => import('./school/school-home.page').then(m => m.SchoolHomePage)
  },
  {
    path: 'school/children/:studentId',
    loadComponent: () => import('./school/school-child.page').then(m => m.SchoolChildPage)
  },
  {
    path: 'statement',
    loadComponent: () => import('./portal-statement-list/portal-statement.list').then(m => m.PortalStatementList)
  }
];

