import { Routes } from '@angular/router';

/**
 * The Hrm pages (H1, TK-48), mounted under the shell by `apps/hrms` and
 * `apps/payroll`. The employee master and organisation setup are open to every
 * app that employs people; announcements and policies are HRMS's alone.
 */
export const hrmRoutes: Routes = [
  {
    path: 'hrm/employees',
    loadComponent: () => import('./employees/employees.list').then((m) => m.EmployeesList),
    data: { access: { permission: 'employee.view' } },
  },
  {
    path: 'hrm/employees/new',
    loadComponent: () => import('./employees/employee.page').then((m) => m.EmployeePage),
    data: { access: { permission: 'employee.edit' } },
  },
  {
    path: 'hrm/employees/:id',
    loadComponent: () => import('./employees/employee.page').then((m) => m.EmployeePage),
    data: { access: { permission: 'employee.view' } },
  },
  {
    path: 'hrm/organisation',
    loadComponent: () => import('./organisation/organisation.page').then((m) => m.OrganisationPage),
    data: { access: { permission: 'employee.view' } },
  },
  {
    path: 'hrm/announcements',
    loadComponent: () => import('./notices/announcements.page').then((m) => m.AnnouncementsPage),
    data: { access: { permission: 'hrm.view', apps: ['Hrms'] } },
  },
  {
    path: 'hrm/policies',
    loadComponent: () => import('./notices/policies.page').then((m) => m.PoliciesPage),
    data: { access: { permission: 'hrm.view', apps: ['Hrms'] } },
  },
];
