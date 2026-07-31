import { Routes } from '@angular/router';
import {
  AcceptInvitationPage,
  ForgotPasswordPage,
  LoginPage,
  SignupPage,
  TrialExpiredPage,
  authGuard,
  licenseActiveGuard,
} from '@bill-book/auth';
import { ShellComponent } from '@bill-book/app-shell';
import { DashboardPage } from './dashboard/dashboard.page';

export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'accept-invitation', component: AcceptInvitationPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  {
    path: '',
    component: ShellComponent,
    // licenseActiveGuard sits above every feature route: an expired licence
    // lands on /expired no matter what URL is typed.
    canActivate: [authGuard, licenseActiveGuard],
    canActivateChild: [licenseActiveGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage },
      {
        path: 'settings/currencies',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.OrgCurrenciesPage),
      },
      {
        path: 'settings/configuration',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.ConfigurationsPage),
      },
      {
        path: 'settings/roles',
        loadComponent: () => import('@bill-book/identity-ui').then((m) => m.RolesPage),
      },
      {
        path: 'settings/users',
        loadComponent: () => import('@bill-book/identity-ui').then((m) => m.UsersPage),
      },
      {
        path: 'settings/email',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.SmtpSettingsPage),
      },
      {
        path: 'accounting/chart-of-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ChartOfAccountsPage),
      },
      {
        path: 'accounting/sub-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.SubAccountsPage),
      },
      {
        path: 'settings/tax',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TaxMasterPage),
      },
      {
        path: 'settings/numbering',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.NumberingSeriesPage),
      },
      {
        path: 'settings/payment-terms',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.PaymentTermsPage),
      },
      {
        path: 'contacts',
        loadComponent: () => import('@bill-book/contacts-ui').then((m) => m.ContactsPage),
      },
      {
        path: 'inventory/items',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.ItemsPage),
      },
      {
        path: 'inventory/categories',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.ItemCategoriesPage),
      },
      {
        path: 'inventory/warehouses',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.WarehousesPage),
      },
      {
        path: 'settings/unit-types',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.UnitTypesPage),
      },
      {
        path: 'settings/metal-purities',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.MetalPuritiesPage),
      },
      // Feature modules mount here as they are built:
      // sales, purchase, banking, contacts, inventory, accounting, reports
      { path: '**', component: DashboardPage },
    ],
  },
];
