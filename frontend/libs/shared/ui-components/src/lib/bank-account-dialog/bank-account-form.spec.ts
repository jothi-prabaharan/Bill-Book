import { describe, expect, it } from 'vitest';
import {
  BANK_ACCOUNT_KINDS,
  BankAccountRecord,
  accountRequestFrom,
  blankAccountForm,
  kindCanOverdraw,
  changeAccountKind,
  findBankByName,
  accountFormFromRecord,
  accountFormProblem,
  allowedKinds,
  kindLedgerNote,
  kindNeedsBank,
} from './bank-account-form';

/*
 * The decisions behind the bank and cash account popup (TK-268). Written by an
 * AI and not run (section 0.5 of docs/Task_Details.md).
 */

const record = (over: Partial<BankAccountRecord> = {}): BankAccountRecord => ({
  bankAccountId: 7,
  bankId: 3,
  accountName: 'HDFC Current',
  accountNumber: '5010012345',
  accountType: 'Current',
  ifsc: 'HDFC0000123',
  micr: '600240002',
  swiftCode: 'HDFCINBB',
  iban: null,
  branchName: 'Anna Nagar',
  currencyCode: 'INR',
  odLimit: null,
  isActive: true,
  ...over,
});

describe('the bank and cash account popup (TK-268)', () => {
  it('BAF-01 starts a new account as a current account in rupees, active, with no bank chosen', () => {
    const form = blankAccountForm();

    expect(form.accountType).toBe('Current');
    expect(form.currencyCode).toBe('INR');
    expect(form.isActive).toBe(true);
    expect(form.bankId).toBeNull();
    expect(form.accountName).toBe('');
    expect(form.accountNumber).toBe('');
  });

  it('BAF-02 lays what the opener presets over the defaults, and a cash preset carries no bank detail', () => {
    const cash = blankAccountForm({ accountType: 'Cash', accountName: 'Cash in hand', accountNumber: 'CASH', bankId: 9, ifsc: 'X', odLimit: 5 });

    expect(cash.accountType).toBe('Cash');
    expect(cash.accountName).toBe('Cash in hand');
    expect(cash.accountNumber).toBe('CASH');
    // A bank, an IFSC and a limit mean nothing to cash in hand, so a preset cannot carry them in.
    expect(cash.bankId).toBeNull();
    expect(cash.ifsc).toBe('');
    expect(cash.odLimit).toBeNull();
  });

  it('BAF-03 only cash in hand and a wallet go without a bank', () => {
    expect(BANK_ACCOUNT_KINDS.filter((k) => !kindNeedsBank(k.value)).map((k) => k.value)).toEqual(['Wallet', 'Cash']);
    expect(kindNeedsBank('Current')).toBe(true);
    expect(kindNeedsBank('Savings')).toBe(true);
    expect(kindNeedsBank('CreditCard')).toBe(true);
    expect(kindNeedsBank('EEFC')).toBe(true);
  });

  it('BAF-04 only an overdraft, cash credit and a credit card take a limit', () => {
    expect(BANK_ACCOUNT_KINDS.filter((k) => kindCanOverdraw(k.value)).map((k) => k.value)).toEqual([
      'OverDraft',
      'CashCredit',
      'CreditCard',
    ]);
  });

  it('BAF-05 says which ledger account each kind creates', () => {
    expect(kindLedgerNote('Cash')).toBe('Creates an Asset account under Cash in Hand.');
    expect(kindLedgerNote('Wallet')).toBe('Creates an Asset account under Cash in Hand.');
    expect(kindLedgerNote('Current')).toBe('Creates an Asset account under Bank Accounts.');
    expect(kindLedgerNote('Savings')).toBe('Creates an Asset account under Bank Accounts.');
    expect(kindLedgerNote('OverDraft')).toContain('Liability');
    expect(kindLedgerNote('CreditCard')).toContain('Bank OD & Credit Cards');
  });

  it('BAF-06 offers every kind, or only the kinds the opener allows, in the usual order', () => {
    expect(allowedKinds(null)).toHaveLength(8);
    expect(allowedKinds(['Wallet', 'Current']).map((k) => k.value)).toEqual(['Current', 'Wallet']);
    expect(allowedKinds(['Cash']).map((k) => k.label)).toEqual(['Cash in hand']);
    expect(allowedKinds([])).toEqual([]);
  });

  it('BAF-07 changing to a kind with no bank drops the bank, its details and any limit', () => {
    const bank = accountFormFromRecord(record({ accountType: 'OverDraft', odLimit: 50000 }));
    const cash = changeAccountKind(bank, 'Cash');

    expect(cash.accountType).toBe('Cash');
    expect(cash.bankId).toBeNull();
    expect(cash.ifsc).toBe('');
    expect(cash.micr).toBe('');
    expect(cash.swiftCode).toBe('');
    expect(cash.branchName).toBe('');
    expect(cash.odLimit).toBeNull();
    // The name and number the person typed stay.
    expect(cash.accountName).toBe('HDFC Current');
    expect(cash.accountNumber).toBe('5010012345');
  });

  it('BAF-08 changing between two bank kinds keeps the bank and drops only a limit that no longer applies', () => {
    const overdraft = accountFormFromRecord(record({ accountType: 'OverDraft', odLimit: 50000 }));
    const savings = changeAccountKind(overdraft, 'Savings');

    expect(savings.bankId).toBe(3);
    expect(savings.ifsc).toBe('HDFC0000123');
    expect(savings.odLimit).toBeNull();
    expect(changeAccountKind(overdraft, 'CashCredit').odLimit).toBe(50000);
  });

  it('BAF-09 turns an existing account into a form, a missing optional field into an empty string', () => {
    const form = accountFormFromRecord(record());

    expect(form.iban).toBe('');
    expect(form.ifsc).toBe('HDFC0000123');
    expect(form.bankId).toBe(3);
    expect(form.accountType).toBe('Current');
  });

  it('BAF-10 names the first thing missing, in the order the fields read', () => {
    const complete = blankAccountForm({ accountName: 'HDFC Current', accountNumber: '5010012345', bankId: 3 });

    expect(accountFormProblem(blankAccountForm(), null)).toBe('Account name is required.');
    expect(accountFormProblem({ ...complete, accountName: '   ' }, null)).toBe('Account name is required.');
    expect(accountFormProblem({ ...complete, accountNumber: '' }, null)).toBe('Account number is required.');
    expect(accountFormProblem({ ...complete, bankId: null }, null)).toBe('Choose a bank for this account type.');
    expect(accountFormProblem({ ...complete, currencyCode: 'RS' }, null)).toBe('Currency must be a 3-letter code.');
    expect(accountFormProblem(complete, null)).toBeNull();
  });

  it('BAF-11 accepts a bank typed in place of one chosen, and refuses a blank one', () => {
    const typed = blankAccountForm({ accountName: 'HDFC Current', accountNumber: '5010012345' });

    expect(accountFormProblem(typed, 'HDFC Bank')).toBeNull();
    expect(accountFormProblem(typed, '   ')).toBe('Enter the name of the bank.');
    expect(accountFormProblem(typed, null)).toBe('Choose a bank for this account type.');
  });

  it('BAF-12 asks cash in hand for no bank, and still wants its name and a reference', () => {
    const cash = blankAccountForm({ accountType: 'Cash' });

    expect(accountFormProblem(cash, null)).toBe('Account name is required.');
    expect(accountFormProblem({ ...cash, accountName: 'Cash in hand' }, null)).toBe('Account number is required.');
    expect(accountFormProblem({ ...cash, accountName: 'Cash in hand', accountNumber: 'CASH' }, null)).toBeNull();
  });

  it('BAF-13 refuses a negative limit on a kind that has one', () => {
    const overdraft = blankAccountForm({ accountType: 'OverDraft', accountName: 'OD', accountNumber: '1', bankId: 3, odLimit: -1 });

    expect(accountFormProblem(overdraft, null)).toBe('The limit cannot be negative.');
    expect(accountFormProblem({ ...overdraft, odLimit: 0 }, null)).toBeNull();
  });

  it('BAF-14 builds the request trimmed, with codes in capitals and a blank optional field as null', () => {
    const body = accountRequestFrom({
      ...blankAccountForm(),
      bankId: 3,
      accountName: '  HDFC Current ',
      accountNumber: ' 5010012345 ',
      ifsc: ' hdfc0000123 ',
      swiftCode: 'hdfcinbb',
      micr: '  ',
      iban: '',
      branchName: ' Anna Nagar ',
      currencyCode: ' inr ',
    });

    expect(body).toEqual({
      bankId: 3,
      accountName: 'HDFC Current',
      accountNumber: '5010012345',
      accountType: 'Current',
      ifsc: 'HDFC0000123',
      micr: null,
      swiftCode: 'HDFCINBB',
      iban: null,
      branchName: 'Anna Nagar',
      currencyCode: 'INR',
      odLimit: null,
      isActive: true,
    });
  });

  it('BAF-15 sends cash in hand with no bank and no bank detail even if the form once held some', () => {
    const body = accountRequestFrom({
      ...blankAccountForm({ accountType: 'Cash', accountName: 'Cash in hand', accountNumber: 'CASH' }),
      bankId: 3,
      ifsc: 'HDFC0000123',
      odLimit: 100,
    });

    expect(body.bankId).toBeNull();
    expect(body.ifsc).toBeNull();
    expect(body.odLimit).toBeNull();
    expect(body.accountType).toBe('Cash');
  });

  it('BAF-16 sends a limit only for the kinds that carry one', () => {
    const overdraft = { ...blankAccountForm({ accountType: 'OverDraft', accountName: 'OD', accountNumber: '1', bankId: 3 }), odLimit: 250000 };

    expect(accountRequestFrom(overdraft).odLimit).toBe(250000);
    expect(accountRequestFrom({ ...overdraft, accountType: 'Current' }).odLimit).toBeNull();
  });

  it('BAF-17 finds a bank by the name typed, ignoring case and space, and none for a blank or unknown name', () => {
    const banks = [
      { bankId: 1, bankName: 'State Bank of India' },
      { bankId: 2, bankName: 'HDFC Bank' },
    ];

    expect(findBankByName(banks, '  hdfc bank ')).toBe(2);
    expect(findBankByName(banks, 'STATE BANK OF INDIA')).toBe(1);
    expect(findBankByName(banks, 'Axis Bank')).toBeNull();
    expect(findBankByName(banks, '   ')).toBeNull();
    expect(findBankByName([], 'HDFC Bank')).toBeNull();
  });
});
