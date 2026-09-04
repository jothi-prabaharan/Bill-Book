import { describe, expect, it } from 'vitest';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  formatDate,
  formatMoney,
  formatNumber,
  groupSizesFromMask,
} from './format-settings';

describe('groupSizesFromMask', () => {
  it('reads lakh-crore grouping off the rupee mask', () => {
    expect(groupSizesFromMask('##,##,##0.00')).toEqual([3, 2]);
  });

  it('reads thousands off a western mask', () => {
    expect(groupSizesFromMask('###,###,##0.00')).toEqual([3, 3]);
  });

  it('reports no grouping when the mask has no separator', () => {
    // Not a fallback to threes: a mask of 0.00 is asking for 1234567.00, and
    // grouping it anyway would overrule the data the mask exists to carry.
    expect(groupSizesFromMask('0.00')).toEqual([]);
  });
});

describe('formatNumber', () => {
  it('groups by lakh and crore, not by millions', () => {
    // The whole reason the mask is read rather than assumed: 1234567 is
    // 12,34,567 on an Indian invoice and 1,234,567 on an American one.
    expect(formatNumber(1234567, 2, '##,##,##0.00')).toBe('12,34,567.00');
    expect(formatNumber(1234567, 2, '###,###,##0.00')).toBe('1,234,567.00');
  });

  it('groups a crore correctly', () => {
    expect(formatNumber(12345678, 0, '##,##,##0.00')).toBe('1,23,45,678');
  });

  it('leaves a number shorter than the first group ungrouped', () => {
    expect(formatNumber(999, 2, '##,##,##0.00')).toBe('999.00');
  });

  it('does not group at all when the mask asks for no separator', () => {
    expect(formatNumber(1234567, 2, '0.00')).toBe('1234567.00');
  });

  it('keeps the minus outside the grouping', () => {
    expect(formatNumber(-1234567, 2, '##,##,##0.00')).toBe('-12,34,567.00');
  });

  it('renders nothing for null rather than a zero that means something', () => {
    expect(formatNumber(null, 2)).toBe('');
    expect(formatNumber(undefined, 2)).toBe('');
    expect(formatNumber(Number.NaN, 2)).toBe('');
  });
});

describe('formatMoney', () => {
  const suffixed: FormatSettings = {
    ...DEFAULT_FORMAT_SETTINGS,
    currencySymbol: 'kr',
    symbolPosition: 'Suffix',
    currencyMask: '###,###,##0.00',
  };

  it('puts a prefix symbol against the digits', () => {
    expect(formatMoney(1234.5, DEFAULT_FORMAT_SETTINGS)).toBe('₹1,234.50');
  });

  it('puts a suffix symbol after a space', () => {
    expect(formatMoney(1234.5, suffixed)).toBe('1,234.50 kr');
  });

  it('keeps the minus left of a prefix symbol', () => {
    // "-₹100.00" reads as a negative amount; "₹-100.00" reads as a typo.
    expect(formatMoney(-100, DEFAULT_FORMAT_SETTINGS)).toBe('-₹100.00');
  });
});

describe('formatDate', () => {
  it('renders a DateOnly in the branch pattern', () => {
    expect(formatDate('2026-09-04', 'dd/MM/yyyy')).toBe('04/09/2026');
  });

  it('does not shift the day in a western timezone', () => {
    // new Date('2026-09-04') is midnight UTC, which is 3 September anywhere
    // west of Greenwich. An invoice date that moves by a day depending on who
    // is looking is a defect that reaches a filed return, so the string is
    // split rather than parsed.
    const previous = process.env.TZ;
    process.env.TZ = 'America/Los_Angeles';
    try {
      expect(formatDate('2026-09-04', 'dd/MM/yyyy')).toBe('04/09/2026');
    } finally {
      process.env.TZ = previous;
    }
  });

  it('supports a short month without leaving a stray M behind', () => {
    expect(formatDate('2026-09-04', 'dd-MMM-yyyy')).toBe('04-Sep-2026');
  });

  it('supports a two-digit year', () => {
    expect(formatDate('2026-09-04', 'dd/MM/yy')).toBe('04/09/26');
  });

  it('takes the date half of a timestamp rather than rendering blank', () => {
    expect(formatDate('2026-09-04T13:45:00Z', 'dd/MM/yyyy')).toBe('04/09/2026');
  });

  it('returns the input unchanged when it is not a date at all', () => {
    // Degrading to visible wrong-looking text beats degrading to a wrong date.
    expect(formatDate('not a date', 'dd/MM/yyyy')).toBe('not a date');
  });

  it('renders nothing for null', () => {
    expect(formatDate(null)).toBe('');
    expect(formatDate(undefined)).toBe('');
  });
});
