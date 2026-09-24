import { describe, expect, it } from 'vitest';
import { TenderLine, roundToRupee, tenderSummary } from './pos-tender';

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
