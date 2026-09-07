import { describe, expect, it } from 'vitest';
import { parseDecimalParts, scaledToText, stepFor, textToScaled } from './decimal';

/**
 * The conversion between what a person types and what the form holds.
 *
 * These are the tests that matter most in the whole input system: every money
 * figure in the product passes through `textToScaled`, and the sales and
 * purchase documents hold theirs as integer paise, where being out by one is
 * being out by a paisa on a ledger that has to balance.
 */
describe('decimal', () => {
  describe('parseDecimalParts', () => {
    it('DEC-01: splits a plain decimal into its parts', () => {
      expect(parseDecimalParts('1250.50')).toEqual({
        negative: false,
        integer: '1250',
        fraction: '50',
      });
    });

    it('DEC-02: accepts a value pasted from a formatted display', () => {
      // Indian grouping, which is what `formatMoney` renders.
      expect(parseDecimalParts('12,34,567.89')).toEqual({
        negative: false,
        integer: '1234567',
        fraction: '89',
      });
    });

    it('DEC-03: reads a leading point as a zero integer part', () => {
      expect(parseDecimalParts('.5')).toEqual({ negative: false, integer: '0', fraction: '5' });
    });

    it('DEC-04: half-typed values are null, not zero', () => {
      // Treating these as zero would fight the person typing: the field would
      // publish 0 the moment they pressed the minus key.
      expect(parseDecimalParts('')).toBeNull();
      expect(parseDecimalParts('-')).toBeNull();
      expect(parseDecimalParts('.')).toBeNull();
      expect(parseDecimalParts('abc')).toBeNull();
      expect(parseDecimalParts('12abc')).toBeNull();
      expect(parseDecimalParts('1.2.3')).toBeNull();
    });

    it('DEC-05: carries the sign separately from the digits', () => {
      expect(parseDecimalParts('-42.7')).toEqual({
        negative: true,
        integer: '42',
        fraction: '7',
      });
    });
  });

  describe('textToScaled', () => {
    it('DEC-10: at zero scale the decimal is the value', () => {
      expect(textToScaled('1250.50', 0)).toBe(1250.5);
      expect(textToScaled('0', 0)).toBe(0);
      expect(textToScaled('-15.5', 0)).toBe(-15.5);
    });

    it('DEC-11: at scale two the value is whole paise', () => {
      expect(textToScaled('1250.50', 2)).toBe(125050);
      expect(textToScaled('0.01', 2)).toBe(1);
      expect(textToScaled('0', 2)).toBe(0);
    });

    it('DEC-12: no floating-point corruption on the values that produce it', () => {
      // parseFloat('1.15') * 100 is 114.99999999999999, and 0.1 + 0.2 is not
      // 0.3. The conversion here is string surgery, so neither happens.
      expect(textToScaled('1.15', 2)).toBe(115);
      expect(textToScaled('0.29', 2)).toBe(29);
      expect(textToScaled('8.20', 2)).toBe(820);
      expect(textToScaled('1.005', 2)).toBe(101);
      expect(textToScaled('16.08', 2)).toBe(1608);
      expect(textToScaled('1.005', 3)).toBe(1005);
    });

    it('DEC-13: excess precision rounds half away from zero, as the ledger does', () => {
      // Half up and half away from zero differ on exactly the negative halves,
      // and a round-off line is signed.
      expect(textToScaled('1.005', 2)).toBe(101);
      expect(textToScaled('-1.005', 2)).toBe(-101);
      expect(textToScaled('2.345', 2)).toBe(235);
      expect(textToScaled('-2.345', 2)).toBe(-235);
    });

    it('DEC-14: rounding carries into the integer part', () => {
      expect(textToScaled('9.999', 2)).toBe(1000);
      expect(textToScaled('0.999', 2)).toBe(100);
      expect(textToScaled('99.995', 2)).toBe(10000);
    });

    it('DEC-15: a quantity at six places, which is what the line grid holds', () => {
      expect(textToScaled('1', 6)).toBe(1_000_000);
      expect(textToScaled('2.5', 6)).toBe(2_500_000);
      expect(textToScaled('0.125', 6)).toBe(125_000);
      expect(textToScaled('0.000001', 6)).toBe(1);
    });

    it('DEC-16: an exchange rate keeps all eight of its places', () => {
      // `ExchangeRate` is decimal(18,8); a place dropped here is a discrepancy
      // multiplied across every amount on the document.
      expect(textToScaled('83.12345678', 0)).toBe(83.12345678);
      expect(textToScaled('0.01123456', 0)).toBe(0.01123456);
    });

    it('DEC-17: half-typed text publishes null rather than a number', () => {
      expect(textToScaled('', 2)).toBeNull();
      expect(textToScaled('-', 2)).toBeNull();
      expect(textToScaled('not_a_number', 2)).toBeNull();
    });

    it('DEC-18: negative zero is plain zero', () => {
      // -0 serialises as "-0" and is not Object.is-equal to 0, which is a
      // difference nobody wants leaking into a form value.
      expect(Object.is(textToScaled('-0.00', 2), 0)).toBe(true);
      expect(Object.is(textToScaled('-0', 0), 0)).toBe(true);
    });
  });

  describe('scaledToText', () => {
    it('DEC-20: paise read back exactly, with the point inserted not divided', () => {
      expect(scaledToText(125050, 2, 2)).toBe('1250.50');
      expect(scaledToText(1, 2, 2)).toBe('0.01');
      expect(scaledToText(0, 2, 2)).toBe('0.00');
      expect(scaledToText(-125050, 2, 2)).toBe('-1250.50');
    });

    it('DEC-21: `decimals` is a floor, so nothing the value carries is hidden', () => {
      // Truncating the display of a value the field is editing is how somebody
      // saves a rounded figure believing they saw the whole one.
      expect(scaledToText(1_234_567, 6, 2)).toBe('1.234567');
      expect(scaledToText(1_000_000, 6, 2)).toBe('1.00');
      expect(scaledToText(2_500_000, 6, 2)).toBe('2.50');
    });

    it('DEC-22: at zero scale it pads to the floor and keeps what is there', () => {
      expect(scaledToText(1250.5, 0, 2)).toBe('1250.50');
      expect(scaledToText(42, 0, 0)).toBe('42');
      expect(scaledToText(12.345, 0, 0)).toBe('12.345');
      expect(scaledToText(83.12345678, 0, 8)).toBe('83.12345678');
    });

    it('DEC-23: nothing is rendered for nothing', () => {
      expect(scaledToText(null, 2, 2)).toBe('');
      expect(scaledToText(undefined, 2, 2)).toBe('');
      expect(scaledToText(Number.NaN, 2, 2)).toBe('');
    });

    it('DEC-24: a round trip through both directions is lossless', () => {
      const typed = ['0', '0.01', '1250.50', '99999.99', '1.15', '8.20'];
      for (const text of typed) {
        const stored = textToScaled(text, 2);
        expect(stored).not.toBeNull();
        expect(scaledToText(stored, 2, 2)).toBe(Number(text).toFixed(2));
      }
    });
  });

  describe('stepFor', () => {
    it('DEC-30: the step follows the displayed precision, not the stored scale', () => {
      // The browser compares `step` against the element's own value, which is
      // the decimal on screen.
      expect(stepFor(0)).toBe(1);
      expect(stepFor(2)).toBe(0.01);
      expect(stepFor(6)).toBe(0.000001);
      expect(stepFor(8)).toBe(1e-8);
    });
  });
});
