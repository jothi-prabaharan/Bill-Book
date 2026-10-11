import { Routes } from '@angular/router';
import { HomePageComponent } from './home/home-page.component';

export const appRoutes: Routes = [
  {
    path: '',
    component: HomePageComponent,
    title: 'Bill-Book — Retail ERP, POS & Indian Accounting SaaS',
  },
  {
    path: '**',
    redirectTo: '',
  },
];
