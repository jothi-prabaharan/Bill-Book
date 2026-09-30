import { Routes } from '@angular/router';

export const payrollRoutes: Routes = [
  {
    path: 'payroll/components',
    loadComponent: () => import('./components/salary-components.page').then((m) => m.SalaryComponentsPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'payroll/runs',
    loadComponent: () => import('./runs/payroll-runs.list').then((m) => m.PayrollRunsList),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'payroll/runs/:id',
    loadComponent: () => import('./runs/payroll-run.page').then((m) => m.PayrollRunPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'payroll/statutory',
    loadComponent: () => import('./statutory/statutory-settings.page').then((m) => m.StatutorySettingsPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'payroll/tax',
    loadComponent: () => import('./tax/tax-declarations.page').then((m) => m.TaxDeclarationsPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'payroll/fnf',
    loadComponent: () => import('./fnf/fnf-settlement.page').then((m) => m.FnfSettlementPage),
    data: { access: { permission: 'payroll.view', addOns: ['Payroll'] } },
  },
  {
    path: 'me/payslips',
    loadComponent: () => import('./self-service/my-payslips.page').then((m) => m.MyPayslipsPage),
    data: { access: { signedIn: true, addOns: ['Payroll'] } },
  },
];
