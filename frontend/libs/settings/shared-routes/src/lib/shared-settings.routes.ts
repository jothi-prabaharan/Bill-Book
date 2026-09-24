import { Routes } from '@angular/router';

/**
 * The settings pages every app mounts (H0.3/H0.6, TK-47): one list, one lib,
 * so no app carries a copy of a shared page's route or of its access rule.
 *
 * Each page is its own lib under `libs/settings/`, loaded lazily. All of them
 * open on `settings.view`, which every app's roles may hold, and save on
 * `settings.edit`; the server checks both, and its controllers name every app
 * (`[RequireApp(App.All)]`).
 *
 * The retail-only settings (tax master, payment terms, closing dates, units,
 * HSN/SAC, metal purity, contact person roles) stay in `apps/web`'s own routes.
 */
export const sharedSettingsRoutes: Routes = [
  {
    path: 'settings/currencies',
    loadComponent: () => import('@bill-book/settings-currencies').then((m) => m.OrgCurrenciesPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/organization',
    loadComponent: () =>
      import('@bill-book/settings-organization-settings').then((m) => m.OrganizationSettingsPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/branches',
    loadComponent: () => import('@bill-book/settings-organizations').then((m) => m.OrganizationsPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/configuration',
    loadComponent: () => import('@bill-book/settings-configuration').then((m) => m.ConfigurationsPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/roles',
    loadComponent: () => import('@bill-book/settings-roles').then((m) => m.RolesPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/users',
    loadComponent: () => import('@bill-book/settings-users').then((m) => m.UsersPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/api-clients',
    loadComponent: () => import('@bill-book/settings-api-clients').then((m) => m.ApiClientsListComponent),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/email',
    loadComponent: () => import('@bill-book/settings-smtp').then((m) => m.SmtpSettingsPage),
    data: { access: { permission: 'settings.view' } },
  },
  // One menu row per document type, each landing on its own type. Opening
  // takes settings.view; the page is read-only without settings.edit, and
  // the API refuses a write without it regardless.
  {
    path: 'settings/print-templates',
    loadComponent: () => import('@bill-book/settings-print-templates').then((m) => m.PrintTemplatesPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/print-templates/:docType',
    loadComponent: () => import('@bill-book/settings-print-templates').then((m) => m.PrintTemplatesPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/numbering',
    loadComponent: () => import('@bill-book/settings-numbering-series').then((m) => m.NumberingSeriesPage),
    data: { access: { permission: 'settings.view' } },
  },
  {
    path: 'settings/applications',
    loadComponent: () => import('@bill-book/settings-applications').then((m) => m.ApplicationsPage),
    data: { access: { permission: 'settings.view' } },
  },
];
