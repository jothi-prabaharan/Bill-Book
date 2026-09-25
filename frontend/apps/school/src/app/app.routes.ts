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
import { hrmRoutes } from '@bill-book/hrm-ui';
import { sharedSettingsRoutes } from '@bill-book/settings-shared-routes';
import { sisRoutes } from '@bill-book/sis-ui';
import { admissionRoutes } from '@bill-book/admission-ui';
import { HomePage } from './home/home.page';

/**
 * The School app's routes (S0, TK-60). Sign-in, signup and the expired page
 * are the shared auth pages; everything else is under the shell, which
 * attaches its own guards. The shared settings and the employee master are
 * mounted, never copied; the HRMS-only pages among the employee routes name
 * their app, so the shell refuses them here. School's own screens are added
 * as each stage is built (S1 onward).
 */
export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: 'accept-invitation', component: AcceptInvitationPage },
  { path: 'expired', component: TrialExpiredPage, canActivate: [authGuard] },
  shellRoutes({
    app: 'School',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', component: HomePage, data: { access: { signedIn: true } } },
      // Guardians and maintenance vendors are contacts (TK-60): the same
      // page RetailErp uses, with the Guardians filter.
      {
        path: 'contacts',
        loadComponent: () => import('@bill-book/master-ui').then((m) => m.ContactsPage),
        data: { access: { permission: 'contacts.view' } },
      },
      ...hrmRoutes,
      // Students, academic setup, exams and marks (S1, TK-61).
      ...sisRoutes,
      // Enquiries, applications and admitting (S2, TK-62).
      ...admissionRoutes,
      ...sharedSettingsRoutes,
      { path: '**', component: HomePage, data: { access: { signedIn: true } } },
    ],
  }),
];
