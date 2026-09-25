import { describe, expect, it } from 'vitest';
import { attendancePercent, totalDue } from './school-portal.models';

describe('school portal models (TK-69)', () => {
  it('add up what is owed', () => {
    expect(totalDue([{ balance: 1500 }, { balance: 0 }, { balance: 250.5 }] as never)).toBe(1750.5);
  });

  it('count a late day as present, a half day as half, and holidays as nothing', () => {
    expect(attendancePercent({ present: 17, absent: 1, late: 1, halfDay: 2, leave: 1 })).toBe(86);
    expect(attendancePercent({ present: 0, absent: 0, late: 0, halfDay: 0, leave: 0 })).toBeNull();
  });
});
