import { AbstractControl } from '@angular/forms';
import { BbRadioOption } from '@bill-book/ui-components';
import { OtpChannel, SignupCodeResponse, SignupResponse } from './auth.models';

/**
 * The pieces the signup and forgot-password pages share for one-time codes
 * (TK-196, TK-197). Pure: no DOM, no HTTP, so they are tested without a page.
 */

/** Email first and chosen unless the person picks otherwise, on both pages (TK-197). */
export const DEFAULT_OTP_CHANNEL: OtpChannel = 'Email';

/**
 * The same three on signup and on password reset: email to the address, or
 * WhatsApp or SMS to the mobile number.
 */
export const OTP_CHANNELS: readonly BbRadioOption<OtpChannel>[] = [
  { value: 'Email', label: 'Email' },
  { value: 'WhatsApp', label: 'WhatsApp' },
  { value: 'Sms', label: 'SMS' },
];

/** True for the channels that send to the mobile number, which then has to be there. */
export function isPhoneChannel(channel: OtpChannel): boolean {
  return channel === 'WhatsApp' || channel === 'Sms';
}

/** "on WhatsApp", "by SMS", "by email": the phrase after "sent". */
export function channelPhrase(channel: OtpChannel): string {
  switch (channel) {
    case 'Sms':
      return 'by SMS';
    case 'Email':
      return 'by email';
    default:
      return 'on WhatsApp';
  }
}

/** True when the signup answered with a code on its way rather than a created trial. */
export function isSignupCodeSent(response: SignupResponse | SignupCodeResponse): response is SignupCodeResponse {
  return !('customerId' in response);
}

/** Whole seconds until `until` (epoch ms), never below zero. Null means no wait. */
export function secondsLeft(until: number | null, now: number): number {
  return until === null ? 0 : Math.max(0, Math.ceil((until - now) / 1000));
}

/** A wait as `m:ss`, the way a resend countdown reads. */
export function formatWait(seconds: number): string {
  const whole = Math.max(0, Math.ceil(seconds));
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`;
}

/** The six digits a code is, matching the API's `^\d{6}$`. */
export const OTP_CODE_PATTERN = /^\d{6}$/;

/**
 * What to show under a field: its first broken rule in words, once the field
 * has been touched, else nothing. Each field shows its own; the message box is
 * for what the server says.
 */
export function fieldError(
  control: AbstractControl,
  label: string,
  words: Partial<Record<string, string>> = {},
): string {
  if (!control.touched || !control.errors) {
    return '';
  }

  const [rule, detail] = Object.entries(control.errors)[0];
  if (words[rule]) {
    return words[rule] as string;
  }

  switch (rule) {
    case 'required':
      return `${label} is required.`;
    case 'email':
    case 'bbEmail':
      return 'Enter a valid email address.';
    case 'minlength':
      return `${label} must be at least ${(detail as { requiredLength: number }).requiredLength} characters.`;
    case 'maxlength':
      return `${label} cannot exceed ${(detail as { requiredLength: number }).requiredLength} characters.`;
    case 'bbPhone':
      return `${label} is too long.`;
    default:
      return `Check the ${label.toLowerCase()}.`;
  }
}

/**
 * The sentence to show for a refused request: the server's own `message`, the
 * first of its validation errors, or, for the rate limiter's bare 429, a line
 * saying to wait.
 */
export function apiMessage(err: unknown, fallback: string): string {
  const failure = err as {
    status?: number;
    error?: { message?: string; errors?: Record<string, string[]> } | null;
  };

  const message = failure?.error?.message;
  if (message) {
    return message;
  }

  const firstError = Object.values(failure?.error?.errors ?? {})[0]?.[0];
  if (firstError) {
    return firstError;
  }

  if (failure?.status === 429) {
    return 'Too many tries from this connection. Wait a minute and try again.';
  }

  return fallback;
}

/** The wait the server asked for on a refused resend, in seconds, if it named one. */
export function retryAfterSeconds(err: unknown): number | null {
  const seconds = (err as { error?: { retryAfterSeconds?: number } | null })?.error?.retryAfterSeconds;
  return typeof seconds === 'number' && seconds > 0 ? seconds : null;
}
