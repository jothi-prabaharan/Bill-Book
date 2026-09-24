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
import { HomePage } from './home/home.page';

/**
 * The Payroll app's routes (TK-47). Sign-in, signup and the expired page are
 * the shared auth pages; everything else is under the shell, which attaches
 * its own guards. Its own screens are added here as each stage is built
 * (H1 onward).
 */
export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'accept-invitation', component: AcceptInvitationPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  shellRoutes({
    app: 'Payroll',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: HomePage, data: { access: { signedIn: true } } },
      ...sharedSettingsRoutes,
      { path: '**', component: HomePage, data: { access: { signedIn: true } } },
    ],
  }),
];
