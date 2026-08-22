import { describe, expect, it } from 'vitest';
import { amountInWords } from './amount-in-words';

/**
 * The words on an invoice exist to corroborate the figure beside them, so the
 * cases worth pinning are the ones where the two could quietly disagree: the
 * Indian grouping, the rounding, and zero paise.
 */
describe('amountInWords', () => {
  it('AIW-01: groups by lakh and crore, not by million', () => {
    // The whole reason this is not Intl.NumberFormat. Western grouping would
    // say "one million two hundred thirty four thousand".
    expect(amountInWords(1234567)).toBe(
      'Rupees twelve lakh thirty four thousand five hundred sixty seven only',
    );

    expect(amountInWords(10000000)).toBe('Rupees one crore only');
    expect(amountInWords(100000)).toBe('Rupees one lakh only');
  });

  it('AIW-02: spells the paise, and only when there are any', () => {
    expect(amountInWords(1234.5)).toBe(
      'Rupees one thousand two hundred thirty four and fifty paise only',
    );

    expect(amountInWords(1234)).toBe('Rupees one thousand two hundred thirty four only');
  });

  it('AIW-03: rounds to the paisa once, so the words match the printed figure', () => {
    // 1234.505 prints as 1234.51, and has to read as it prints — rounding the
    // rupees and the paise separately is how those two come apart.
    expect(amountInWords(1234.505)).toBe(
      'Rupees one thousand two hundred thirty four and fifty one paise only',
    );

    // Rounds up into the next rupee rather than saying "and one hundred paise".
    expect(amountInWords(9.999)).toBe('Rupees ten only');
  });

  it('AIW-04: the teens and the round tens are the irregular cases', () => {
    expect(amountInWords(11)).toBe('Rupees eleven only');
    expect(amountInWords(19)).toBe('Rupees nineteen only');
    expect(amountInWords(20)).toBe('Rupees twenty only');
    expect(amountInWords(70)).toBe('Rupees seventy only');
    expect(amountInWords(115)).toBe('Rupees one hundred fifteen only');
  });

  it('AIW-05: zero is spelled, not left blank', () => {
    expect(amountInWords(0)).toBe('Rupees zero only');
  });

  it('AIW-06: a negative amount says so rather than printing as a debit', () => {
    // The currency word keeps its capital, as it does in the positive case —
    // it is a name, not the start of the sentence.
    expect(amountInWords(-500)).toBe('Minus Rupees five hundred only');
  });

  it('AIW-07: the currency word is the caller’s, for a document not in rupees', () => {
    expect(amountInWords(42.25, 'Dollars', 'cents')).toBe(
      'Dollars forty two and twenty five cents only',
    );
  });

  it('AIW-08: a non-finite amount produces nothing rather than "NaN"', () => {
    expect(amountInWords(Number.NaN)).toBe('');
    expect(amountInWords(Number.POSITIVE_INFINITY)).toBe('');
  });
});
