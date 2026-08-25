import { Route } from '@angular/router';
import { LoginPage } from '@bill-book/auth';
import { platformGuard } from './permission.guard';

export const appRoutes: Route[] = [
  { path: 'login', component: LoginPage },
  {
    path: '',
    canActivate: [platformGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'customers' },
      {
        path: 'customers',
        loadComponent: () => import('./customers/customers.page').then((m) => m.CustomersPage),
      },
      {
        path: 'customers/:customerId',
        loadComponent: () =>
          import('./customers/customer-detail.page').then((m) => m.CustomerDetailPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
