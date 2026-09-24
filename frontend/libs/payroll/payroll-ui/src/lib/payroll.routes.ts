import { Routes } from '@angular/router';

export const payrollRoutes: Routes = [
  {
    path: 'payroll/components',
    loadComponent: () => import('./components/salary-components.page').then((m) => m.SalaryComponentsPage),
    data: { access: { permission: 'payroll.view' } },
  },
  {
    path: 'payroll/runs',
    loadComponent: () => import('./runs/payroll-runs.list').then((m) => m.PayrollRunsList),
    data: { access: { permission: 'payroll.view' } },
  },
  {
    path: 'payroll/runs/:id',
    loadComponent: () => import('./runs/payroll-run.page').then((m) => m.PayrollRunPage),
    data: { access: { permission: 'payroll.view' } },
  },
  {
    path: 'payroll/statutory',
    loadComponent: () => import('./statutory/statutory-settings.page').then((m) => m.StatutorySettingsPage),
    data: { access: { permission: 'payroll.view' } },
  },
  {
    path: 'payroll/tax',
    loadComponent: () => import('./tax/tax-declarations.page').then((m) => m.TaxDeclarationsPage),
    data: { access: { permission: 'payroll.view' } },
  },
];
