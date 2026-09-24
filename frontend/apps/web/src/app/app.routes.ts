import { Routes } from '@angular/router';
import {
  AcceptInvitationPage,
  ForgotPasswordPage,
  LoginPage,
  SignupPage,
  TrialExpiredPage,
  authGuard,
} from '@bill-book/auth';
import { shellRoutes } from '@bill-book/app-shell';
import { sharedSettingsRoutes } from '@bill-book/settings-shared-routes';
import { DashboardPage } from './dashboard/dashboard.page';

export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'accept-invitation', component: AcceptInvitationPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  // The shell attaches its own guards (TK-44): an expired licence lands on
  // /expired, a page the user cannot open shows /no-access, and a page that
  // declares no `data.access` is refused. No route here lists a guard.
  shellRoutes({
    app: 'RetailErp',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage, data: { access: { signedIn: true } } },
      // Users, roles, branches, organization settings, currencies,
      // configuration, email, API keys, print templates, numbering and
      // applications: the pages every app shares, from one lib (TK-47).
      ...sharedSettingsRoutes,
      { path: 'accounting', pathMatch: 'full', redirectTo: 'accounting/trial-balance' },
      {
        path: 'accounting/chart-of-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ChartOfAccountsPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/journals',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.JournalsPage),
        data: { access: { permission: 'accounting.view' } },
      },
      // The same page with an entry open, so a posted journal can be linked to
      // from a ledger row.
      {
        path: 'accounting/journals/:journalId',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.JournalsPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/opening-balance',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.OpeningBalancePage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/trial-balance',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TrialBalancePage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/ledger',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.AccountLedgerPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/ledger/:accountId',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.AccountLedgerPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/reconciliation',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.ReconciliationPageComponent),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'accounting/sub-accounts',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.SubAccountsPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        // Settling is per contact, so the id is the route rather than a filter
        // inside the screen — it makes the workspace linkable from a contact,
        // a statement or an aging report.
        path: 'accounting/allocations/:contactId',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.AllocationWorkspacePage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'settings/tax',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TaxMasterPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'settings/contact-person-roles',
        loadComponent: () =>
          import('@bill-book/master-ui').then((m) => m.ContactPersonRolesPage),
        data: { access: { permission: 'contacts.view' } },
      },
      {
        path: 'settings/closing-dates',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.ClosingDatesPage),
        data: { access: { permission: 'accounting.view' } },
      },
      {
        path: 'settings/payment-terms',
        loadComponent: () =>
          import('@bill-book/accounting-ui').then((m) => m.PaymentTermsPage),
        data: { access: { permission: 'accounting.view' } },
      },
      // Leads and Tickets are the two halves the Customer service was merged
      // from, and they kept their original permission modules: the catalogue
      // seeds crm.* and support.*, never customer.*. These guards name the same
      // modules LeadsController and TicketsController demand, so a role that
      // cannot open the page cannot reach the API behind it either.
      {
        path: 'customer/leads',
        loadComponent: () => import('@bill-book/customer-ui').then((m) => m.LeadList),
        data: { access: { permission: 'crm.view' } },
      },
      {
        path: 'customer/tickets',
        loadComponent: () => import('@bill-book/customer-ui').then((m) => m.TicketList),
        data: { access: { permission: 'support.view' } },
      },
      {
        path: 'contacts',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.ContactsPage),
        data: { access: { permission: 'contacts.view' } },
      },
      // The nav rail points at /inventory, so it needs somewhere to land. Items
      // is the primary feature of the inventory module.
      { path: 'inventory', pathMatch: 'full', redirectTo: 'inventory/items' },
      {
        path: 'inventory/items',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.ItemsPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'inventory/categories',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.ItemCategoriesPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'inventory/stock',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.StockPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'inventory/stock-adjustments',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.StockAdjustmentsPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'inventory/price-lists',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.PriceListListComponent),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'inventory/warehouses',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.WarehousesPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'settings/unit-types',
        loadComponent: () => import('@bill-book/inventory-ui').then((m) => m.UnitTypesPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'settings/hsn-sac',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.HsnSacPage),
        data: { access: { permission: 'inventory.view' } },
      },
      {
        path: 'settings/metal-purities',
        loadComponent: () =>
          import('@bill-book/inventory-ui').then((m) => m.MetalPuritiesPage),
        data: { access: { permission: 'inventory.view' } },
      },
      // The nav rail points at /banking, so it needs somewhere to land. Spend
      // money is the right default: it is the screen this module is opened for.
      { path: 'banking', pathMatch: 'full', redirectTo: 'banking/spend-money' },
      {
        path: 'banking/banks',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.BanksPage),
        data: { access: { permission: 'banking.view' } },
      },
      {
        path: 'banking/accounts',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.BankAccountsPage),
        data: { access: { permission: 'banking.view' } },
      },
      // Spend and receive are the same document read in opposite directions, so
      // they are one component told which way round it is. Two routes rather
      // than one with a toggle, because they are two things a user goes looking
      // for by name.
      {
        path: 'banking/spend-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.MoneyDocumentPage),
        data: { access: { permission: 'banking.view' }, direction: 'spend' },
      },
      {
        path: 'banking/receive-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.MoneyDocumentPage),
        data: { access: { permission: 'banking.view' }, direction: 'receive' },
      },
      {
        path: 'banking/statements',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.StatementsPage),
        data: { access: { permission: 'banking.view' } },
      },
      {
        path: 'banking/transfer-money',
        loadComponent: () => import('@bill-book/accounting-ui').then((m) => m.TransferMoneyPage),
        data: { access: { permission: 'banking.view' } },
      },
      // Feature modules mount here as they are built:
      // sales, purchase, banking, contacts, inventory, accounting, reports
      // A lazy module's parent declares access for every child that declares
      // none of its own: the sales screens all take sales.view.
      {
        path: 'sales',
        loadChildren: () => import('@bill-book/sales-ui').then((m) => m.salesRoutes),
        data: { access: { permission: 'sales.view' } },
      },
      {
        path: 'purchase',
        loadChildren: () =>
          import('@bill-book/purchase-ui').then((m) => m.purchaseRoutes),
        data: { access: { permission: 'purchase.view' } },
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('@bill-book/reporting-ui').then((m) => m.reportingRoutes),
        data: { access: { permission: 'reports.view' } },
      },
      { path: '**', component: DashboardPage, data: { access: { signedIn: true } } },
    ],
  }),
];
