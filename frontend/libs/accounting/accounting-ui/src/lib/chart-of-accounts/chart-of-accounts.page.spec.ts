import { describe, expect, it } from 'vitest';
import { accountState } from './chart-of-accounts.page';

/** Which tab an account falls under on the chart's register (TK-187). */
describe('Chart of accounts register', () => {
  it('COA-REG-01: an active, unlocked account is Active', () => {
    expect(accountState({ isActive: true, isLock: false })).toBe('Active');
  });

  it('COA-REG-02: an active account with posting locked is Locked', () => {
    expect(accountState({ isActive: true, isLock: true })).toBe('Locked');
  });

  it('COA-REG-03: an inactive account is Inactive, locked or not', () => {
    expect(accountState({ isActive: false, isLock: false })).toBe('Inactive');
    expect(accountState({ isActive: false, isLock: true })).toBe('Inactive');
  });
});
