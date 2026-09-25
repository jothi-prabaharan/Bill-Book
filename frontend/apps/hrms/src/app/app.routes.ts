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
import { employeeRoutes } from '@bill-book/employee-ui';
import { timeLeaveRoutes } from '@bill-book/time-leave-ui';
import { claimsRoutes } from '@bill-book/claims-ui';
import { recruitmentRoutes } from '@bill-book/recruitment-ui';
import { performanceRoutes } from '@bill-book/performance-ui';
import { sharedSettingsRoutes } from '@bill-book/settings-shared-routes';
import { HomePage } from './home/home.page';

/**
 * The HRMS app's routes (TK-47). Sign-in, signup and the expired page are
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
    app: 'Hrms',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: HomePage, data: { access: { signedIn: true } } },
      // The employee master and organisation setup (TK-48); the HRMS-only
      // pages among them name their app, and the shell refuses them elsewhere.
      ...employeeRoutes,
      ...timeLeaveRoutes,
      ...claimsRoutes,
      ...recruitmentRoutes,
      ...performanceRoutes,
      ...sharedSettingsRoutes,
      { path: '**', component: HomePage, data: { access: { signedIn: true } } },
    ],
  }),
];
