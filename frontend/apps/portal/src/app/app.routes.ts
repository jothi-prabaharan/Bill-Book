import { Route } from '@angular/router';

export const appRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./statement.page').then(m => m.StatementPage)
  }
];
