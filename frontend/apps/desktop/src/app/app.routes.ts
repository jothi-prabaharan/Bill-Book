import { Routes } from '@angular/router';
import { LoginPage, SignupPage, ForgotPasswordPage, authGuard } from '@bill-book/auth';
import { PosTerminalComponent } from './pos-terminal/pos-terminal.component';

export const appRoutes: Routes = [
  { path: 'login', component: LoginPage },
  { path: 'signup', component: SignupPage },
  { path: 'forgot-password', component: ForgotPasswordPage },
  { path: '', component: PosTerminalComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];
