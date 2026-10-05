import { describe, expect, it } from 'vitest';
import { TenderDraft, billRequest, changeDue, defaultAccount, tenderProblem, tipProblem } from './restaurant-billing';

const cash = (amount: number): TenderDraft => ({ mode: 'Cash', amount, bankAccountId: 1, reference: '' });
const card = (amount: number): TenderDraft => ({ mode: 'Card', amount, bankAccountId: 2, reference: '' });

describe('restaurant billing', () => {
  it('gives change only from cash', () => {
    expect(tenderProblem(500, [cash(600)])).toBeNull();
    expect(changeDue(500, [cash(600)])).toBe(100);
    expect(tenderProblem(500, [card(600)])).toContain('Only cash gives change');
    expect(tenderProblem(500, [card(300), cash(250)])).toBeNull();
    expect(changeDue(500, [card(300), cash(250)])).toBe(50);
  });

  it('says how much is still to pay', () => {
    expect(tenderProblem(500, [cash(200)])).toContain('300.00 is still to pay');
    expect(changeDue(500, [cash(200)])).toBeNull();
  });

  it('bills on account with no tenders', () => {
    expect(tenderProblem(500, [])).toBeNull();
  });

  it('needs an account for every payment and the tip', () => {
    expect(tenderProblem(500, [{ ...cash(500), bankAccountId: null }])).toContain('account');
    expect(tipProblem(true, 50, null)).toBe('Choose where the tip was paid in.');
    expect(tipProblem(false, 50, 1)).toContain('switched off');
    expect(tipProblem(false, 0, null)).toBeNull();
  });

  it('defaults cash to the cash account and cards to a bank', () => {
    const accounts = [
      { bankAccountId: 7, accountName: 'HDFC', maskedNumber: '1234', isCash: false },
      { bankAccountId: 3, accountName: 'Cash', maskedNumber: '', isCash: true },
    ];
    expect(defaultAccount('Cash', accounts)).toBe(3);
    expect(defaultAccount('Upi', accounts)).toBe(7);
  });

  it('sends the chosen dishes only when it is a split', () => {
    const draft = { orderIds: [1], lineIds: [10, 11], contactId: null, applyServiceCharge: true, tipAmount: null, tipBankAccountId: 4, tenders: [] };
    expect(billRequest(draft, [10, 11]).lineIds).toEqual([]);
    expect(billRequest({ ...draft, lineIds: [10] }, [10, 11]).lineIds).toEqual([10]);
    expect(billRequest(draft, [10, 11]).tipBankAccountId).toBeNull();
  });
});
