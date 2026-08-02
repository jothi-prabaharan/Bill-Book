import { Routes } from '@angular/router';
import {
  AcceptInvitationPage,
  ForgotPasswordPage,
  LoginPage,
  SignupPage,
  TrialExpiredPage,
  authGuard,
  licenseActiveGuard,
  permissionGuard,
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
    // lands on /expired no matter what URL is typed. permissionGuard does the
    // same for a screen the role cannot open — the menu no longer offers it,
    // but a typed URL or an old bookmark still arrives.
    canActivate: [authGuard, licenseActiveGuard],
    canActivateChild: [licenseActiveGuard, permissionGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage },
      {
        path: 'settings/currencies',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.OrgCurrenciesPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/branches',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.OrganizationsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/configuration',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.ConfigurationsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/roles',
        loadComponent: () => import('@bill-book/identity-ui').then((m) => m.RolesPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/users',
        loadComponent: () => import('@bill-book/identity-ui').then((m) => m.UsersPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/email',
        loadComponent: () =>
          import('@bill-book/platform-ui').then((m) => m.SmtpSettingsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'accounting/chart-of-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ChartOfAccountsPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/sub-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.SubAccountsPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'settings/tax',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TaxMasterPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'settings/numbering',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.NumberingSeriesPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'settings/payment-terms',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.PaymentTermsPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'contacts',
        loadComponent: () => import('@bill-book/contacts-ui').then((m) => m.ContactsPage),
        data: { permission: 'contacts.view' },
      },
      {
        path: 'inventory/items',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.ItemsPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'inventory/categories',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.ItemCategoriesPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'inventory/stock',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.StockPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'inventory/warehouses',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.WarehousesPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'settings/unit-types',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.UnitTypesPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'settings/metal-purities',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.MetalPuritiesPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'banking/banks',
        loadComponent: () => import('@bill-book/banking-ui').then((m) => m.BanksPage),
        data: { permission: 'banking.view' },
      },
      {
        path: 'banking/accounts',
        loadComponent: () => import('@bill-book/banking-ui').then((m) => m.BankAccountsPage),
        data: { permission: 'banking.view' },
      },
      // Feature modules mount here as they are built:
      // sales, purchase, banking, contacts, inventory, accounting, reports
      { path: '**', component: DashboardPage },
    ],
  },
];
