import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth.service';

/**
 * OTP wizard: request → verify → reset. The request step always advances,
 * whether or not the account exists — never reveal which.
 */
@Component({
  selector: 'bb-forgot-password-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './forgot-password.page.html',
  styleUrl: './forgot-password.page.scss',
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
