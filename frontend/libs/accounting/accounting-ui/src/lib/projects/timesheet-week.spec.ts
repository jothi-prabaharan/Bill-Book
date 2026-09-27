import { describe, expect, it } from 'vitest';
import { dayTotals, isoDate, parseIsoDate, timerHours, TimeEntryRow, weekDays, weekGrid, weekStart } from './timesheet-week';

function entry(partial: Partial<TimeEntryRow>): TimeEntryRow {
  return {
    timeEntryId: 1,
    projectId: 1,
    projectCode: 'PRJ-1',
    projectName: 'Fit-out',
    projectTaskId: 10,
    taskName: 'Design',
    userId: 'u',
    workDate: '2026-09-21',
    hours: 1,
    notes: null,
    isBillable: true,
    billingStatus: 'Unbilled',
    ...partial,
  };
}

describe('timesheet week', () => {
  it('starts a week on Monday, including from a Sunday', () => {
    expect(isoDate(weekStart(parseIsoDate('2026-09-27')))).toBe('2026-09-21');
    expect(isoDate(weekStart(parseIsoDate('2026-09-21')))).toBe('2026-09-21');
    expect(isoDate(weekStart(parseIsoDate('2026-09-23')))).toBe('2026-09-21');
  });

  it('lists seven days across a month end', () => {
    expect(weekDays(parseIsoDate('2026-09-28'))).toEqual([
      '2026-09-28',
      '2026-09-29',
      '2026-09-30',
      '2026-10-01',
      '2026-10-02',
      '2026-10-03',
      '2026-10-04',
    ]);
  });

  it('folds entries into one row per task, summing a day without float drift', () => {
    const days = weekDays(parseIsoDate('2026-09-21'));
    const rows = weekGrid(
      [
        entry({ timeEntryId: 1, hours: 0.1 }),
        entry({ timeEntryId: 2, hours: 0.2 }),
        entry({ timeEntryId: 3, workDate: '2026-09-23', hours: 2 }),
        entry({ timeEntryId: 4, projectTaskId: 11, taskName: 'Build', hours: 3 }),
        entry({ timeEntryId: 5, workDate: '2026-09-30', hours: 8 }),
      ],
      days,
    );

    expect(rows.map((r) => r.label)).toEqual(['PRJ-1 · Build', 'PRJ-1 · Design']);
    expect(rows[1].hours).toEqual([0.3, 0, 2, 0, 0, 0, 0]);
    expect(rows[1].total).toBe(2.3);
    expect(dayTotals(rows)).toEqual([3.3, 0, 2, 0, 0, 0, 0]);
  });

  it('rounds a timer up to the quarter hour, at least one quarter, at most a day', () => {
    expect(timerHours(0)).toBe(0.25);
    expect(timerHours(16 * 60 * 1000)).toBe(0.5);
    expect(timerHours(60 * 60 * 1000)).toBe(1);
    expect(timerHours(30 * 60 * 60 * 1000)).toBe(24);
  });
});
