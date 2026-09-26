import { Route } from '@angular/router';
import { portalSessionGuard } from './portal-session.guard';

export const appRoutes: Route[] = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'login',
    loadComponent: () => import('@bill-book/auth').then(m => m.LoginPage)
  },
  {
    // Where a portal link lands: exchanges its code for a session, then opens the right portal (TK-94).
    path: 'access/:code',
    loadComponent: () => import('./portal-access.page').then(m => m.PortalAccessPage)
  },
  {
    // The old link shape, `/portal?token=…`: those links stopped working when access became revocable.
    path: 'portal',
    loadComponent: () => import('./portal-access.page').then(m => m.PortalAccessPage)
  },
  {
    path: 'expired',
    loadComponent: () => import('./portal-expired.page').then(m => m.PortalExpiredPage)
  },
  {
    path: 'dashboard',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./portal-dashboard/portal-dashboard.page').then(m => m.PortalDashboardPage)
  },
  {
    path: 'school',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./school/school-home.page').then(m => m.SchoolHomePage)
  },
  {
    path: 'school/children/:studentId',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./school/school-child.page').then(m => m.SchoolChildPage)
  },
  {
    path: 'invoices',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./portal-invoices/portal-invoices.list').then(m => m.PortalInvoicesList)
  },
  {
    path: 'invoices/:id',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./portal-invoices/portal-invoice.page').then(m => m.PortalInvoicePage)
  },
  {
    path: 'quotes',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./portal-quotes/portal-quotes.page').then(m => m.PortalQuotesPage)
  },
  {
    path: 'statement',
    canActivate: [portalSessionGuard],
    loadComponent: () => import('./portal-statement-list/portal-statement.list').then(m => m.PortalStatementList)
  }
];
