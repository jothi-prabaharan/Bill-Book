import { describe, expect, it } from 'vitest';
import { financialYearLabel, financialYearOf, monthLabel, quarterOf, recentFinancialYears } from './tds-math';

describe('tds-math', () => {
  it('names the financial year by its first calendar year', () => {
    expect(financialYearOf('2026-04-01')).toBe(2026);
    expect(financialYearOf('2027-03-31')).toBe(2026);
    expect(financialYearOf('2027-01-15')).toBe(2026);
    expect(financialYearLabel(2026)).toBe('2026-27');
    expect(financialYearLabel(2099)).toBe('2099-00');
  });

  it('puts April to June in the first quarter and January to March in the fourth', () => {
    expect(quarterOf('2026-04-10')).toBe(1);
    expect(quarterOf('2026-06-30')).toBe(1);
    expect(quarterOf('2026-07-01')).toBe(2);
    expect(quarterOf('2026-12-31')).toBe(3);
    expect(quarterOf('2027-02-01')).toBe(4);
  });

  it('labels a month and lists recent years newest first', () => {
    expect(monthLabel('2026-06-01')).toBe('June 2026');
    expect(recentFinancialYears('2026-09-29', 3)).toEqual([2026, 2025, 2024]);
  });
});
