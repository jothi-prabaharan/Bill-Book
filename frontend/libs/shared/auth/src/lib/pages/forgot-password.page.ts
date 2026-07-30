import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

/**
 * OTP wizard: request → verify → reset. The request step always advances,
 * whether or not the account exists — never reveal which.
 */
@Component({
  selector: 'bb-forgot-password-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-card">
      <h1>Reset password</h1>

      @switch (step()) {
        @case ('request') {
          <form (ngSubmit)="request()">
            <label>
              Email
              <input name="email" type="email" [(ngModel)]="email" required />
            </label>
            <button type="submit" [disabled]="busy()">Send code</button>
          </form>
        }
        @case ('verify') {
          <p>If the account exists, a 6-digit code was sent. It expires in 10 minutes.</p>
          <form (ngSubmit)="verify()">
            <label>
              Code
              <input name="code" inputmode="numeric" maxlength="6" [(ngModel)]="code" required />
            </label>
            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
            <button type="submit" [disabled]="busy()">Verify</button>
            <button type="button" class="link" (click)="request()">Resend code</button>
          </form>
        }
        @case ('reset') {
          <form (ngSubmit)="reset()">
            <label>
              New password
              <input name="newPassword" type="password" minlength="8" [(ngModel)]="newPassword" required />
            </label>
            <label>
              Confirm password
              <input name="confirmPassword" type="password" [(ngModel)]="confirmPassword" required />
            </label>
            @if (error()) {
              <p class="error">{{ error() }}</p>
            }
            <button type="submit" [disabled]="busy()">Reset password</button>
          </form>
        }
      }

      <p><a routerLink="/login">Back to sign in</a></p>
    </div>
  `,
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  email = '';
  code = '';
  newPassword = '';
  confirmPassword = '';

  protected readonly step = signal<'request' | 'verify' | 'reset'>('request');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async request(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.forgotPassword(this.email);
    } finally {
      // Always advance — identical behaviour whether or not the account exists.
      this.step.set('verify');
      this.busy.set(false);
    }
  }

  async verify(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.verifyOtp(this.email, this.code);
      this.step.set('reset');
    } catch {
      this.error.set('Invalid or expired code.');
    } finally {
      this.busy.set(false);
    }
  }

  async reset(): Promise<void> {
    if (this.newPassword !== this.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.resetPassword(this.email, this.code, this.newPassword, this.confirmPassword);
      await this.router.navigateByUrl('/login');
    } catch {
      this.error.set('Invalid or expired code.');
    } finally {
      this.busy.set(false);
    }
  }
}
