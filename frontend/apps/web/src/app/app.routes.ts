import { Routes } from '@angular/router';
import {
  ForgotPasswordPage,
  LoginPage,
  SignupPage,
  TrialExpiredPage,
  authGuard,
  licenseActiveGuard,
} from '@bill-book/auth';
import { ShellComponent } from '@bill-book/app-shell';
import { DashboardPage } from './dashboard.page';

export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  {
    path: '',
    component: ShellComponent,
    // licenseActiveGuard sits above every feature route: an expired licence
    // lands on /expired no matter what URL is typed.
    canActivate: [authGuard, licenseActiveGuard],
    canActivateChild: [licenseActiveGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: DashboardPage },
      // Feature modules mount here as they are built:
      // sales, purchase, banking, contacts, inventory, accounting, reports, settings
      { path: '**', component: DashboardPage },
    ],
  },
];
