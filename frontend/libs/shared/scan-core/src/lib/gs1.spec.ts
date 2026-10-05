import { describe, expect, it } from 'vitest';
import { gtinCandidates, parseGs1 } from './gs1';

const GS = '\u001d';

describe('parseGs1 (TK-162)', () => {
  it('reads GTIN, expiry and batch from a camera-read DataMatrix', () => {
    expect(parseGs1(`]d2010890123456789017270331` + `10B12${GS}21S0001`)).toEqual({
      gtin: '08901234567890',
      batch: 'B12',
      expiry: '2027-03-31',
      productionDate: null,
      serial: 'S0001',
    });
  });

  it('reads the bracketed form printed under the symbol', () => {
    const data = parseGs1('(01)08901234567890(11)260115(17)280200(10)LOT-7');
    expect(data?.gtin).toBe('08901234567890');
    expect(data?.productionDate).toBe('2026-01-15');
    expect(data?.batch).toBe('LOT-7');
  });

  it('takes a day of 00 as the last day of the month', () => {
    expect(parseGs1('0108901234567890172802' + '00')?.expiry).toBe('2028-02-29');
  });

  it('a variable field at the end needs no separator', () => {
    expect(parseGs1('010890123456789010ABC')?.batch).toBe('ABC');
  });

  it('is not GS1 without a GTIN', () => {
    expect(parseGs1('8901234567890')).toBeNull();
    expect(parseGs1('INV-0042')).toBeNull();
    expect(parseGs1('(10)B12')).toBeNull();
    expect(parseGs1('')).toBeNull();
  });

  it('refuses an impossible date rather than inventing one', () => {
    expect(parseGs1('0108901234567890171399' + '31')?.expiry).toBeNull();
  });
});

describe('gtinCandidates', () => {
  it('tries the EAN-13 inside a GTIN-14 first', () => {
    expect(gtinCandidates('08901234567890')).toEqual(['8901234567890', '08901234567890']);
    expect(gtinCandidates('18901234567897')).toEqual(['18901234567897']);
  });
});
