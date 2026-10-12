import { describe, expect, it } from 'vitest';
import { RateState, numberText, rateNote, rateText } from './create-page.model';

/**
 * The create page's read-only fields (TK-192). The wording matters: a number
 * shown as if it were final, or a rate shown with no source, is a promise the
 * page cannot keep.
 */
describe('create page fields', () => {
  it("CPAGE-01: a saved document shows its own number, a new one the series' next, else a promise", () => {
    expect(numberText('INV/2627/00007', false, 'INV/2627/00042')).toBe('INV/2627/00007');
    expect(numberText(null, false, 'INV/2627/00042')).toBe('INV/2627/00042');
    expect(numberText(null, false, null)).toBe('Assigned on save');
  });

  it('CPAGE-02: while the preview loads the number is blank, not "Assigned on save"', () => {
    expect(numberText(null, true, null)).toBe('');
  });

  it('CPAGE-03: a rate shows up to six places with nothing trailing, and a dash for none', () => {
    expect(rateText(83.25, 'found')).toBe('83.25');
    expect(rateText(0.012345678, 'found')).toBe('0.012346');
    expect(rateText(1, 'base')).toBe('1');
    expect(rateText(null, 'missing')).toBe('—');
    expect(rateText(83.25, 'loading')).toBe('');
  });

  const note = (state: RateState, rate = '83.25') =>
    rateNote({ state, currency: 'USD', symbol: '₹', rate, rateDate: '11/10/2026' });

  it("CPAGE-04: the note says where the rate came from, in the design's words", () => {
    expect(note('base')).toBe('Base currency of the organisation');
    expect(note('found')).toBe('1 USD = ₹83.25 · currency master, 11/10/2026');
    expect(note('locked')).toBe('1 USD = ₹83.25 · saved with the document');
  });

  it('CPAGE-05: with no rate on file the note says so, rather than offering one to type', () => {
    expect(note('missing', '—')).toBe('No USD rate on or before this date in the currency master');
    expect(note('failed', '—')).toBe('The currency master could not be read');
    expect(note('locked', '—')).toBe('No rate was saved with this document');
  });
});
