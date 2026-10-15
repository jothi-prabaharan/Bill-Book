import { describe, expect, it } from 'vitest';
import {
  accountState,
  buildRecursiveParentOptions,
  filterEligibleParents,
} from './chart-of-accounts.page';

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

describe('Parent account selection & recursive options', () => {
  const sampleAccounts = [
    {
      accountId: 1,
      accountTypeId: 1, // Asset
      accountCode: '1000',
      accountName: 'Current Assets',
      parentAccountId: null,
      currencyCode: null,
      isContra: false,
      isSystemDefault: false,
      isActive: true,
      isUsed: false,
      isLock: false,
      isJE: false,
      isSales: false,
      isPurchase: false,
      isPayment: false,
      isBank: false,
      isConfigLocked: false,
    },
    {
      accountId: 2,
      accountTypeId: 1, // Asset
      accountCode: '1100',
      accountName: 'Bank Accounts',
      parentAccountId: 1,
      currencyCode: null,
      isContra: false,
      isSystemDefault: false,
      isActive: true,
      isUsed: false,
      isLock: false,
      isJE: false,
      isSales: false,
      isPurchase: false,
      isPayment: false,
      isBank: false,
      isConfigLocked: false,
    },
    {
      accountId: 3,
      accountTypeId: 1, // Asset
      accountCode: '1110',
      accountName: 'HDFC Bank',
      parentAccountId: 2,
      currencyCode: null,
      isContra: false,
      isSystemDefault: false,
      isActive: true,
      isUsed: true, // Used posting account
      isLock: false,
      isJE: true,
      isSales: false,
      isPurchase: false,
      isPayment: true,
      isBank: true,
      isConfigLocked: true,
    },
    {
      accountId: 4,
      accountTypeId: 5, // Expense
      accountCode: '5000',
      accountName: 'Operating Expenses',
      parentAccountId: null,
      currencyCode: null,
      isContra: false,
      isSystemDefault: false,
      isActive: true,
      isUsed: false,
      isLock: false,
      isJE: false,
      isSales: false,
      isPurchase: false,
      isPayment: false,
      isBank: false,
      isConfigLocked: false,
    },
  ];

  it('PAR-01: Rule 4 - only accounts of the exact same accountTypeId are eligible as parents', () => {
    const assetParents = filterEligibleParents(sampleAccounts, 1, null);
    expect(assetParents.every((a) => a.accountTypeId === 1)).toBe(true);
    expect(assetParents.some((a) => a.accountCode === '5000')).toBe(false);

    const expenseParents = filterEligibleParents(sampleAccounts, 5, null);
    expect(expenseParents.every((a) => a.accountTypeId === 5)).toBe(true);
    expect(expenseParents.some((a) => a.accountCode === '5000')).toBe(true);
  });

  it('PAR-02: Rule 3 - used accounts cannot be assigned as parent accounts', () => {
    const eligible = filterEligibleParents(sampleAccounts, 1, null);
    // HDFC Bank is a leaf account with isUsed = true -> MUST NOT be eligible as a parent
    expect(eligible.some((a) => a.accountId === 3)).toBe(false);
    // Unused parent groups (Current Assets, Bank Accounts) are eligible
    expect(eligible.some((a) => a.accountId === 1)).toBe(true);
    expect(eligible.some((a) => a.accountId === 2)).toBe(true);
  });

  it('PAR-03: Rule 4 - self and descendant accounts cannot be parent (prevents cycles)', () => {
    // When editing account 1 (Current Assets), it cannot parent itself, nor can child 2 (Bank Accounts) parent it
    const eligibleFor1 = filterEligibleParents(sampleAccounts, 1, 1);
    expect(eligibleFor1.some((a) => a.accountId === 1)).toBe(false);
    expect(eligibleFor1.some((a) => a.accountId === 2)).toBe(false);
  });

  it('PAR-04: Rule 1 - parent options are formatted recursively with hierarchical indentation', () => {
    const eligible = filterEligibleParents(sampleAccounts, 1, null);
    const options = buildRecursiveParentOptions(eligible);

    expect(options.length).toBe(2);
    // Root level: Current Assets (no indent)
    expect(options[0].value).toBe(1);
    expect(options[0].label).toBe('1000 · Current Assets');
    // Child level: Bank Accounts (indented with non-breaking space and branch symbol)
    expect(options[1].value).toBe(2);
    expect(options[1].label).toContain('↳ 1100 · Bank Accounts');
  });
});

