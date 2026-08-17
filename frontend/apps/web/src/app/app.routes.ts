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
          import('@bill-book/master-ui').then((m) => m.OrgCurrenciesPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/organization',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.OrganizationSettingsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/branches',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.OrganizationsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/configuration',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.ConfigurationsPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/roles',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.RolesPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/users',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.UsersPage),
        data: { permission: 'settings.view' },
      },
      {
        path: 'settings/email',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.SmtpSettingsPage),
        data: { permission: 'settings.view' },
      },
      // The nav rail points at /accounting, so it needs somewhere to land. The
      // ledger is the right default: it is the screen every other posting in the
      // product is checked on.
      { path: 'accounting', pathMatch: 'full', redirectTo: 'accounting/trial-balance' },
      {
        path: 'accounting/chart-of-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ChartOfAccountsPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/journals',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.JournalsPage),
        data: { permission: 'accounting.view' },
      },
      // The same page with an entry open, so a posted journal can be linked to
      // from a ledger row.
      {
        path: 'accounting/journals/:journalId',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.JournalsPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/opening-balance',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.OpeningBalancePage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/trial-balance',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TrialBalancePage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/ledger',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.AccountLedgerPage),
        data: { permission: 'accounting.view' },
      },
      {
        path: 'accounting/ledger/:accountId',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.AccountLedgerPage),
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
        path: 'settings/contact-person-roles',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.ContactPersonRolesPage),
        data: { permission: 'contacts.view' },
      },
      {
        path: 'settings/closing-dates',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ClosingDatesPage),
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
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.ContactsPage),
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
        path: 'inventory/stock-adjustments',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.StockAdjustmentsPage),
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
        path: 'settings/hsn-sac',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.HsnSacPage),
        data: { permission: 'inventory.view' },
      },
      {
        path: 'settings/metal-purities',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.MetalPuritiesPage),
        data: { permission: 'inventory.view' },
      },
      // The nav rail points at /banking, so it needs somewhere to land. Spend
      // money is the right default: it is the screen this module is opened for.
      { path: 'banking', pathMatch: 'full', redirectTo: 'banking/spend-money' },
      {
        path: 'banking/banks',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.BanksPage),
        data: { permission: 'banking.view' },
      },
      {
        path: 'banking/accounts',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.BankAccountsPage),
        data: { permission: 'banking.view' },
      },
      // Spend and receive are the same document read in opposite directions, so
      // they are one component told which way round it is. Two routes rather
      // than one with a toggle, because they are two things a user goes looking
      // for by name.
      {
        path: 'banking/spend-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.MoneyDocumentPage),
        data: { permission: 'banking.view', direction: 'spend' },
      },
      {
        path: 'banking/receive-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.MoneyDocumentPage),
        data: { permission: 'banking.view', direction: 'receive' },
      },
      {
        path: 'banking/statements',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.StatementsPage),
        data: { permission: 'banking.view' },
      },
      {
        path: 'banking/transfer-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TransferMoneyPage),
        data: { permission: 'banking.view' },
      },
      // Feature modules mount here as they are built:
      // sales, purchase, banking, contacts, inventory, accounting, reports
      {
        path: 'sales',
        loadChildren: () => import('@bill-book/sales-ui').then((m) => m.salesRoutes),
      },
      {
        path: 'purchase',
        loadChildren: () =>
          import('@bill-book/purchase-ui').then((m) => m.purchaseRoutes),
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('@bill-book/reporting-ui').then((m) => m.reportingRoutes),
      },
      { path: '**', component: DashboardPage },
    ],
  },
];
