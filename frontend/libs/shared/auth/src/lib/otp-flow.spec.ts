import { FormControl, Validators } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import {
  DEFAULT_OTP_CHANNEL,
  OTP_CHANNELS,
  OTP_CODE_PATTERN,
  apiMessage,
  channelPhrase,
  fieldError,
  formatWait,
  isPhoneChannel,
  isSignupCodeSent,
  retryAfterSeconds,
  secondsLeft,
} from './otp-flow';

/** The one-time code pieces the signup and forgot-password pages share (TK-196, TK-197). */

describe('channels', () => {
  it('OTP-01: email is the default and listed first', () => {
    expect(DEFAULT_OTP_CHANNEL).toBe('Email');
    expect(OTP_CHANNELS[0].value).toBe('Email');
  });

  it('OTP-02: both pages offer email, WhatsApp and SMS', () => {
    expect(OTP_CHANNELS.map((c) => c.value)).toEqual(['Email', 'WhatsApp', 'Sms']);
    expect(OTP_CHANNELS.map((c) => c.label)).toEqual(['Email', 'WhatsApp', 'SMS']);
  });

  it('OTP-13: only WhatsApp and SMS need the mobile number', () => {
    expect(isPhoneChannel('WhatsApp')).toBe(true);
    expect(isPhoneChannel('Sms')).toBe(true);
    expect(isPhoneChannel('Email')).toBe(false);
  });

  it('OTP-03: says how the code went', () => {
    expect(channelPhrase('WhatsApp')).toBe('on WhatsApp');
    expect(channelPhrase('Sms')).toBe('by SMS');
    expect(channelPhrase('Email')).toBe('by email');
  });
});

describe('the two signup answers', () => {
  it('OTP-04: a code on its way has no customer; a created trial has one', () => {
    expect(
      isSignupCodeSent({ channel: 'WhatsApp', destination: '******1234', expiresInSeconds: 600, resendAfterSeconds: 60, message: '' }),
    ).toBe(true);
    expect(
      isSignupCodeSent({ channel: 'Email', destination: 'owner@example.com', expiresInSeconds: 600, resendAfterSeconds: 60, message: '' }),
    ).toBe(true);
    expect(isSignupCodeSent({ customerId: 'c1', customerCode: '0000000001', message: '' })).toBe(false);
  });
});

describe('the resend countdown', () => {
  it('OTP-05: whole seconds left, never below zero, and none when there is no wait', () => {
    expect(secondsLeft(10_500, 10_000)).toBe(1);
    expect(secondsLeft(70_000, 10_000)).toBe(60);
    expect(secondsLeft(5_000, 10_000)).toBe(0);
    expect(secondsLeft(null, 10_000)).toBe(0);
  });

  it('OTP-06: reads as m:ss', () => {
    expect(formatWait(60)).toBe('1:00');
    expect(formatWait(45)).toBe('0:45');
    expect(formatWait(5)).toBe('0:05');
    expect(formatWait(0)).toBe('0:00');
  });
});

describe('field errors', () => {
  it('OTP-07: nothing until the field is touched', () => {
    const control = new FormControl('', Validators.required);
    expect(fieldError(control, 'Mobile number')).toBe('');
    control.markAsTouched();
    expect(fieldError(control, 'Mobile number')).toBe('Mobile number is required.');
  });

  it('OTP-08: a code that is not six digits says so in the words given', () => {
    const control = new FormControl('12a', [Validators.required, Validators.pattern(OTP_CODE_PATTERN)]);
    control.markAsTouched();
    expect(fieldError(control, 'The code', { pattern: 'Enter the 6 digits from the message.' })).toBe(
      'Enter the 6 digits from the message.',
    );
    expect(OTP_CODE_PATTERN.test('123456')).toBe(true);
    expect(OTP_CODE_PATTERN.test('12345')).toBe(false);
  });

  it('OTP-09: a password too short names its length; a mismatch takes its own words', () => {
    const short = new FormControl('abc', Validators.minLength(8));
    short.markAsTouched();
    expect(fieldError(short, 'Password')).toBe('Password must be at least 8 characters.');

    const confirm = new FormControl('x');
    confirm.setErrors({ mismatch: true });
    confirm.markAsTouched();
    expect(fieldError(confirm, 'Confirm password', { mismatch: 'Both passwords must match.' })).toBe(
      'Both passwords must match.',
    );
  });
});

describe('what the server said', () => {
  it("OTP-10: the server's message first, then its first validation error, then the fallback", () => {
    expect(apiMessage({ status: 400, error: { message: 'That code is not right.' } }, 'x')).toBe('That code is not right.');
    expect(apiMessage({ status: 400, error: { errors: { MobileNumber: ['Mobile number is required.'] } } }, 'x')).toBe(
      'Mobile number is required.',
    );
    expect(apiMessage({ status: 500, error: null }, 'Signup failed.')).toBe('Signup failed.');
  });

  it("OTP-11: the rate limiter's bare 429 says to wait", () => {
    expect(apiMessage({ status: 429, error: null }, 'x')).toMatch(/Wait a minute/);
  });

  it('OTP-12: a refused resend carries its wait', () => {
    expect(retryAfterSeconds({ error: { retryAfterSeconds: 42 } })).toBe(42);
    expect(retryAfterSeconds({ error: { message: 'x' } })).toBeNull();
    expect(retryAfterSeconds(null)).toBeNull();
  });
});
