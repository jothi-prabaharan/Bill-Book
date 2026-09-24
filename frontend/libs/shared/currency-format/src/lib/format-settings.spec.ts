import { describe, expect, it } from 'vitest';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  daysInMonth,
  formatDate,
  formatMoney,
  formatNumber,
  groupSizesFromMask,
  parseDate,
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

/**
 * `parseDate` is `formatDate` run backwards (TK-23): what a branch types in its
 * own pattern comes back as ISO, and the order of the parts is never guessed.
 */
describe('parseDate', () => {
  const patterns = ['dd/MM/yyyy', 'MM/dd/yyyy', 'yyyy-MM-dd', 'dd-MMM-yyyy', 'dd.MM.yy'];
  const dates = ['2026-09-24', '2024-02-29', '2026-01-01', '2026-12-31'];

  for (const pattern of patterns) {
    for (const iso of dates) {
      it(`round-trips ${iso} through ${pattern}`, () => {
        expect(parseDate(formatDate(iso, pattern), pattern)).toBe(iso);
      });
    }
  }

  it('reads the same text as different days on differently formatted branches', () => {
    expect(parseDate('03/04/2026', 'dd/MM/yyyy')).toBe('2026-04-03');
    expect(parseDate('03/04/2026', 'MM/dd/yyyy')).toBe('2026-03-04');
  });

  it('accepts any separator and single-digit parts', () => {
    expect(parseDate('24-09-2026', 'dd/MM/yyyy')).toBe('2026-09-24');
    expect(parseDate('24.9.2026', 'dd/MM/yyyy')).toBe('2026-09-24');
    expect(parseDate(' 4/9/2026 ', 'dd/MM/yyyy')).toBe('2026-09-04');
    expect(parseDate('24-sep-2026', 'dd-MMM-yyyy')).toBe('2026-09-24');
  });

  it('refuses what is not a real date', () => {
    expect(parseDate('31/02/2026', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('29/02/2025', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('13/13/2026', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('2026-09-24', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('24/09', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('tomorrow', 'dd/MM/yyyy')).toBeNull();
  });

  it('treats blank as no date', () => {
    expect(parseDate('', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate('   ', 'dd/MM/yyyy')).toBeNull();
    expect(parseDate(null, 'dd/MM/yyyy')).toBeNull();
  });

  it('knows the leap years', () => {
    expect(daysInMonth(2024, 2)).toBe(29);
    expect(daysInMonth(2100, 2)).toBe(28);
    expect(daysInMonth(2000, 2)).toBe(29);
    expect(daysInMonth(2026, 9)).toBe(30);
  });
});
