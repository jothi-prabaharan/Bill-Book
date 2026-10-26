import { DEFAULT_FORMAT_SETTINGS, FormatSettings } from '@bill-book/currency-format';
import { describe, expect, it } from 'vitest';
import { reportAmount, reportDate, reportDateTime, reportNumber } from './report-format';

/** How a report writes a date and an amount: the branch's settings, exactly (TK-262). */

const indian: FormatSettings = DEFAULT_FORMAT_SETTINGS;

const dollar: FormatSettings = {
  ...DEFAULT_FORMAT_SETTINGS,
  datePattern: 'yyyy-MM-dd',
  currencyCode: 'USD',
  currencySymbol: '$',
  currencyMask: '###,###,##0.00',
};

const euro: FormatSettings = {
  ...DEFAULT_FORMAT_SETTINGS,
  datePattern: 'dd.MM.yyyy',
  currencyCode: 'EUR',
  currencySymbol: '€',
  symbolPosition: 'Suffix',
  currencyMask: '###,###,##0.00',
};

const yen: FormatSettings = { ...dollar, currencyCode: 'JPY', currencySymbol: '¥', currencyDecimals: 0, datePattern: 'MM/dd/yyyy' };

describe('reportDate', () => {
  it('RF-01: writes a date in the branch’s pattern, whatever that pattern is', () => {
    expect(reportDate('2026-10-01', indian)).toBe('01/10/2026');
    expect(reportDate('2026-10-01', dollar)).toBe('2026-10-01');
    expect(reportDate('2026-10-01', euro)).toBe('01.10.2026');
    expect(reportDate('2026-10-01', yen)).toBe('10/01/2026');
  });

  it('RF-02: takes the date half of a timestamp, leaves what is not a date as it came and writes nothing for nothing', () => {
    expect(reportDate('2026-10-01T14:30:00', indian)).toBe('01/10/2026');
    expect(reportDate('not a date', indian)).toBe('not a date');
    expect(reportDate(null, indian)).toBe('');
    expect(reportDate('', indian)).toBe('');
  });
});

describe('reportDateTime', () => {
  it('RF-03: writes the date in the branch’s pattern and the time as hours and minutes', () => {
    expect(reportDateTime('2026-10-01T14:30:00', indian)).toBe('01/10/2026 14:30');
    expect(reportDateTime('2026-10-01 09:05', euro)).toBe('01.10.2026 09:05');
  });

  it('RF-04: a value with no time is just the date, and nothing is nothing', () => {
    expect(reportDateTime('2026-10-01', indian)).toBe('01/10/2026');
    expect(reportDateTime(null, indian)).toBe('');
  });
});

describe('reportAmount', () => {
  it('RF-05: writes the branch’s symbol, grouping and decimals, and keeps the decimals of a whole amount', () => {
    expect(reportAmount(1234567.5, indian)).toBe('₹12,34,567.50');
    expect(reportAmount(13500, indian)).toBe('₹13,500.00');
    expect(reportAmount(1234567.5, dollar)).toBe('$1,234,567.50');
  });

  it('RF-06: puts the symbol on the side the branch’s currency asks for, and the minus before it', () => {
    expect(reportAmount(1234.5, euro)).toBe('1,234.50 €');
    expect(reportAmount(-1000, indian)).toBe('-₹1,000.00');
  });

  it('RF-07: shows as many decimals as the branch’s currency has', () => {
    expect(reportAmount(1234, yen)).toBe('¥1,234');
  });

  it('RF-08: an amount in another currency is a bare number in the branch’s grouping, to carry its code beside it', () => {
    expect(reportAmount(1234567.5, indian, 'USD')).toBe('12,34,567.50');
    expect(reportAmount(100, indian, 'INR')).toBe('₹100.00');
    expect(reportAmount(100, indian, '')).toBe('₹100.00');
    expect(reportAmount(100, indian, null)).toBe('₹100.00');
  });

  it('RF-09: nothing is nothing, and a nought is a nought', () => {
    expect(reportAmount(null, indian)).toBe('');
    expect(reportAmount(undefined, indian)).toBe('');
    expect(reportAmount(Number.NaN, indian)).toBe('');
    expect(reportAmount(0, indian)).toBe('₹0.00');
  });
});

describe('reportNumber', () => {
  it('RF-10: groups as the branch’s mask says and shows the branch’s decimals, with no symbol', () => {
    expect(reportNumber(1234567.5, indian)).toBe('12,34,567.50');
    expect(reportNumber(1234567.5, dollar)).toBe('1,234,567.50');
    expect(reportNumber(-1234.5, indian)).toBe('-1,234.50');
    expect(reportNumber(1234, yen)).toBe('1,234');
    expect(reportNumber(null, indian)).toBe('');
  });
});
