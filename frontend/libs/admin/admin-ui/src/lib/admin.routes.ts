import { Routes } from '@angular/router';
import { AdminShellComponent } from './admin-ui/admin-ui';
import { platformGuard } from './permission.guard';

export const adminRoutes: Routes = [
  {
    path: 'admin',
    component: AdminShellComponent,
    canActivate: [platformGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'customers' },
      {
        path: 'customers',
        loadComponent: () => import('./customers/customers.page').then((m) => m.CustomersPage),
      },
      {
        path: 'rates',
        loadComponent: () => import('./rates/rates.page').then((m) => m.RatesPage),
      },
      {
        path: 'customers/:customerId',
        loadComponent: () =>
          import('./customers/customer-detail.page').then((m) => m.CustomerDetailPage),
      },
    ]
  }
];
