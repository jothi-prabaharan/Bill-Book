import { ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, computed, inject, signal } from '@angular/core';
import { AbstractControl, ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { OtpChannel } from '../../auth.models';
import { AuthService } from '../../auth.service';
import {
  DEFAULT_OTP_CHANNEL,
  OTP_CODE_PATTERN,
  OTP_CHANNELS,
  apiMessage,
  channelPhrase,
  fieldError,
  formatWait,
  secondsLeft,
} from '../../otp-flow';
import {
  EmailInputComponent,
  MessageBoxComponent,
  PasswordInputComponent,
  RadioGroupComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

/** The server's own wait between codes, kept here too so the button says so rather than silently sending nothing. */
const RESEND_AFTER_MS = 60_000;

/** One sentence for every way a code can fail, so the answer never says whether the account exists. */
const CODE_REFUSED = 'That code is wrong or has expired. Check it, or send a new code.';

/**
 * OTP wizard: request → verify → reset. The request step always advances,
 * whether or not the account exists — never reveal which.
 *
 * The code goes by email unless WhatsApp or SMS is chosen (TK-197); a WhatsApp
 * or SMS code goes to the mobile number on the account (TK-196). A field's own
 * problem shows under it; a refused code shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-forgot-password-page',
  standalone: true,
  imports: [
    AuthShellComponent,
    ReactiveFormsModule,
    RouterLink,
    EmailInputComponent,
    PasswordInputComponent,
    MessageBoxComponent,
    RadioGroupComponent,
    TextInputComponent,
  ],
  templateUrl: './forgot-password.page.html',
  styleUrl: './forgot-password.page.scss',
})
export class ForgotPasswordPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly channelOptions = OTP_CHANNELS;

  protected readonly fpForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    channel: new FormControl<OtpChannel>(DEFAULT_OTP_CHANNEL, { nonNullable: true }),
    code: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(OTP_CODE_PATTERN)] }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] })
  });

  protected readonly step = signal<'request' | 'verify' | 'reset'>('request');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  /** The channel the last code went by, for the verify step's words. */
  protected readonly sentBy = signal<OtpChannel>(DEFAULT_OTP_CHANNEL);

  private readonly resendAt = signal<number | null>(null);
  private readonly now = signal(Date.now());
  protected readonly resendIn = computed(() => secondsLeft(this.resendAt(), this.now()));
  protected readonly resendWait = computed(() => formatWait(this.resendIn()));

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  protected err(control: AbstractControl, label: string, words: Partial<Record<string, string>> = {}): string {
    return fieldError(control, label, words);
  }

  protected sentPhrase(): string {
    return channelPhrase(this.sentBy());
  }

  /** Where the code went, for the verify step's sentence, which has already named the address. */
  protected sentTo(): string {
    return this.sentBy() === 'Email' ? 'that address' : 'the mobile number on that account';
  }

  async request(): Promise<void> {
    const email = this.fpForm.controls.email;
    if (email.invalid) {
      email.markAsTouched();
      return;
    }
    if (this.busy() || this.resendIn() > 0) return;

    const channel = this.fpForm.controls.channel.value;
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.forgotPassword(email.value, channel);
    } catch (err: unknown) {
      // Only the connection's own limit is worth saying; anything else would
      // be a difference between an account and none.
      if ((err as { status?: number })?.status === 429) {
        this.error.set(apiMessage(err, ''));
      }
    } finally {
      // Always advance — identical behaviour whether or not the account exists.
      this.sentBy.set(channel);
      this.resendAt.set(Date.now() + RESEND_AFTER_MS);
      this.fpForm.controls.code.reset('');
      this.step.set('verify');
      this.busy.set(false);
    }
  }

  /** Back to the first step to pick another channel; the resend wait still holds. */
  changeChannel(): void {
    this.error.set(null);
    this.step.set('request');
  }

  async verify(): Promise<void> {
    const code = this.fpForm.controls.code;
    if (code.invalid) {
      code.markAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.verifyOtp(this.fpForm.controls.email.value, code.value);
      this.step.set('reset');
    } catch (err: unknown) {
      this.error.set((err as { status?: number })?.status === 429 ? apiMessage(err, CODE_REFUSED) : CODE_REFUSED);
    } finally {
      this.busy.set(false);
    }
  }

  async reset(): Promise<void> {
    const { code, newPassword, confirmPassword } = this.fpForm.controls;
    if (code.invalid) {
      this.step.set('verify');
      return;
    }
    newPassword.markAsTouched();
    confirmPassword.markAsTouched();
    if (newPassword.invalid || confirmPassword.invalid) return;
    if (newPassword.value !== confirmPassword.value) {
      confirmPassword.setErrors({ mismatch: true });
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.auth.resetPassword(this.fpForm.controls.email.value, code.value, newPassword.value, confirmPassword.value);
      await this.router.navigateByUrl('/login');
    } catch (err: unknown) {
      this.error.set((err as { status?: number })?.status === 429 ? apiMessage(err, CODE_REFUSED) : CODE_REFUSED);
    } finally {
      this.busy.set(false);
    }
  }
}
