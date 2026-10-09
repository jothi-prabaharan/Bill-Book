import { describe, expect, it } from 'vitest';
import { isTrustedUrl } from './trust';

describe('isTrustedUrl', () => {
  it("trusts the app's own scheme and host", () => {
    expect(isTrustedUrl('app://bill-book/sales/invoices', null)).toBe(true);
    expect(isTrustedUrl('app://bill-book', null)).toBe(true);
  });

  it('trusts the dev server only when one is configured', () => {
    expect(isTrustedUrl('http://localhost:4200/login', 'http://localhost:4200')).toBe(true);
    expect(isTrustedUrl('http://localhost:4200/login', null)).toBe(false);
  });

  it('trusts nothing else', () => {
    expect(isTrustedUrl('app://evil/index.html', null)).toBe(false);
    expect(isTrustedUrl('https://bill-book/index.html', null)).toBe(false);
    expect(isTrustedUrl('file:///etc/passwd', null)).toBe(false);
    expect(isTrustedUrl('http://localhost:4201', 'http://localhost:4200')).toBe(false);
    expect(isTrustedUrl(undefined, null)).toBe(false);
    expect(isTrustedUrl('nonsense', null)).toBe(false);
  });
});
