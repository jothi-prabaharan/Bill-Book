import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth.service';

/**
 * OTP wizard: request → verify → reset. The request step always advances,
 * whether or not the account exists — never reveal which.
 */
@Component({
  selector: 'bb-forgot-password-page',
  standalone: true,
  imports: [AuthShellComponent, ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.page.html',
  styleUrl: './forgot-password.page.scss',
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly fpForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    code: new FormControl('', { nonNullable: true, validators: Validators.required }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] })
  });

  protected readonly step = signal<'request' | 'verify' | 'reset'>('request');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  async request(): Promise<void> {
    if (this.fpForm.controls.email.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.forgotPassword(this.fpForm.value.email!);
    } finally {
      // Always advance — identical behaviour whether or not the account exists.
      this.step.set('verify');
      this.busy.set(false);
    }
  }

  async verify(): Promise<void> {
    if (this.fpForm.controls.code.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.verifyOtp(this.fpForm.value.email!, this.fpForm.value.code!);
      this.step.set('reset');
    } catch {
      this.error.set('Invalid or expired code.');
    } finally {
      this.busy.set(false);
    }
  }

  async reset(): Promise<void> {
    if (this.fpForm.controls.code.invalid) return;
    if (this.fpForm.value.newPassword !== this.fpForm.value.confirmPassword) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.resetPassword(this.fpForm.value.email!, this.fpForm.value.code!, this.fpForm.value.newPassword!, this.fpForm.value.confirmPassword!);
      await this.router.navigateByUrl('/login');
    } catch {
      this.error.set('Invalid or expired code.');
    } finally {
      this.busy.set(false);
    }
  }
}
