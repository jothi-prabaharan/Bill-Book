import { describe, expect, it } from 'vitest';
import {
  TenderLine,
  accountFitsMode,
  newAccountHeading,
  newAccountPreset,
  roundToRupee,
  tenderAccountKinds,
  tenderAccountNotice,
  tenderSummary,
} from './pos-tender';

/** Paying for a till sale, with the server's rules (TK-39, TK-40). Amounts in paise. */
describe('tenderSummary', () => {
  const cash = (amount: number): TenderLine => ({ mode: 'Cash', amount, bankAccountId: 1 });
  const card = (amount: number): TenderLine => ({ mode: 'Card', amount, bankAccountId: 2 });

  it('gives change from cash', () => {
    const summary = tenderSummary(11_800, [cash(20_000)]);

    expect(summary.problem).toBeNull();
    expect(summary.change).toBe(8_200);
    expect(summary.remaining).toBe(0);
  });

  it('says what is still to pay when the tenders fall short', () => {
    const summary = tenderSummary(11_800, [card(5_000)]);

    expect(summary.problem).not.toBeNull();
    expect(summary.remaining).toBe(6_800);
  });

  it('refuses card over the total, because only cash gives change', () => {
    expect(tenderSummary(11_800, [card(12_000)]).problem).toContain('Only cash gives change');
  });

  it('accepts a split with change taken from cash', () => {
    const summary = tenderSummary(11_800, [card(10_000), cash(2_000)]);

    expect(summary.problem).toBeNull();
    expect(summary.change).toBe(200);
  });

  it('needs at least one tender', () => {
    expect(tenderSummary(11_800, []).problem).toBe('Add a tender.');
  });
});

describe('roundToRupee', () => {
  it('rounds the total to the rupee, as the invoice round-off does', () => {
    expect(roundToRupee(11_849)).toBe(11_800);
    expect(roundToRupee(11_850)).toBe(11_900);
    expect(roundToRupee(11_800)).toBe(11_800);
  });
});

/** The accounts a tender goes into, and adding one from the till (TK-268). */
describe('the accounts a tender goes into', () => {
  it('PTA-01 puts cash in a cash account and card and UPI in any other', () => {
    expect(accountFitsMode('Cash', { accountType: 'Cash' })).toBe(true);
    expect(accountFitsMode('Cash', { accountType: 'Current' })).toBe(false);
    expect(accountFitsMode('Card', { accountType: 'Current' })).toBe(true);
    expect(accountFitsMode('Upi', { accountType: 'Wallet' })).toBe(true);
    expect(accountFitsMode('Card', { accountType: 'Cash' })).toBe(false);
    expect(accountFitsMode('Upi', { accountType: 'Cash' })).toBe(false);
  });

  it('PTA-02 offers cash in hand for cash, and for card and UPI only the kinds the till lists', () => {
    expect(tenderAccountKinds('Cash')).toEqual(['Cash']);
    expect(tenderAccountKinds('Card')).toEqual(['Current', 'Savings', 'Wallet', 'EEFC']);
    expect(tenderAccountKinds('Upi')).toEqual(tenderAccountKinds('Card'));
  });

  it('PTA-03 never offers a kind the server leaves out of the till list', () => {
    const left = ['OverDraft', 'CashCredit', 'CreditCard'];

    for (const mode of ['Cash', 'Card', 'Upi'] as const) {
      expect(tenderAccountKinds(mode).filter((kind) => left.includes(kind))).toEqual([]);
    }
  });

  it('PTA-04 every kind offered for a mode is one that fits it', () => {
    for (const mode of ['Cash', 'Card', 'Upi'] as const) {
      for (const kind of tenderAccountKinds(mode)) {
        expect(accountFitsMode(mode, { accountType: kind })).toBe(true);
      }
    }
  });

  it('PTA-05 starts a cash account as cash in hand with a reference, and any other as a blank current account', () => {
    expect(newAccountPreset('Cash')).toEqual({ accountType: 'Cash', accountName: 'Cash in hand', accountNumber: 'CASH' });
    expect(newAccountPreset('Card')).toEqual({ accountType: 'Current', accountName: '', accountNumber: '' });
    expect(newAccountPreset('Upi').accountType).toBe('Current');
  });

  it('PTA-06 names the popup for what is being added', () => {
    expect(newAccountHeading('Cash')).toBe('Add a cash account');
    expect(newAccountHeading('Card')).toBe('Add a bank account');
    expect(newAccountHeading('Upi')).toBe('Add a bank account');
  });

  it('PTA-07 tells the cashier what is missing, and who can add it', () => {
    expect(tenderAccountNotice('Cash', true)).toBe('There is no cash account yet. Use + beside Into to add one.');
    expect(tenderAccountNotice('Card', true)).toBe('There is no bank account for card payments yet. Use + beside Into to add one.');
    expect(tenderAccountNotice('Upi', false)).toBe(
      'There is no bank account for UPI payments yet. Ask the owner to add one under Banking › Bank accounts.',
    );
  });
});
