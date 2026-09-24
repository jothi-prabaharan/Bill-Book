import { describe, expect, it } from 'vitest';
import { addDays, addMonths, monthGrid, outOfRange, todayIso, weekdayIndex } from './calendar';

/** The calendar behind `bb-branch-date-input` (TK-23). */
describe('calendar', () => {
  it('steps days across month and year ends', () => {
    expect(addDays('2026-09-30', 1)).toBe('2026-10-01');
    expect(addDays('2026-01-01', -1)).toBe('2025-12-31');
    expect(addDays('2024-02-28', 1)).toBe('2024-02-29');
  });

  it('steps months and pulls a day back into a shorter month', () => {
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
    expect(addMonths('2024-01-31', 1)).toBe('2024-02-29');
    expect(addMonths('2026-12-15', 1)).toBe('2027-01-15');
    expect(addMonths('2026-03-15', -12)).toBe('2025-03-15');
  });

  it('counts weekdays from Monday', () => {
    expect(weekdayIndex('2026-09-21')).toBe(0); // a Monday
    expect(weekdayIndex('2026-09-27')).toBe(6); // a Sunday
  });

  it('lays a month out in whole Monday-first weeks', () => {
    const weeks = monthGrid('2026-09-24');

    expect(weeks.every((w) => w.length === 7)).toBe(true);
    expect(weeks[0][0].iso).toBe('2026-08-31');
    expect(weeks[0][1]).toEqual({ iso: '2026-09-01', day: 1, inMonth: true });
    expect(weeks.flat().filter((d) => d.inMonth)).toHaveLength(30);
    expect(weeks[weeks.length - 1][6].iso).toBe('2026-10-04');
  });

  it('reads today from the local calendar', () => {
    expect(todayIso(new Date(2026, 8, 24, 23, 59))).toBe('2026-09-24');
  });

  it('compares a range as text', () => {
    expect(outOfRange('2026-09-24', '2026-09-25', null)).toBe(true);
    expect(outOfRange('2026-09-24', null, '2026-09-23')).toBe(true);
    expect(outOfRange('2026-09-24', '2026-09-01', '2026-09-30')).toBe(false);
    expect(outOfRange('2026-09-24', null, null)).toBe(false);
  });
});
