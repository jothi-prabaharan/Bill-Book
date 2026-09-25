import { describe, expect, it } from 'vitest';
import { allPresent, mayEdit, withMark } from './register-rules';
import { ATTENDANCE_MARKS, RegisterRow } from './student-attendance.models';

const rows: RegisterRow[] = [
  { enrolmentId: 1, rollNo: 1, admissionNo: 'ADM-1', studentName: 'Arun', attendanceStatus: 'Absent', remarks: null },
  { enrolmentId: 2, rollNo: 2, admissionNo: 'ADM-2', studentName: 'Meera', attendanceStatus: 'Late', remarks: null },
];

describe('register rules (TK-63)', () => {
  it('mark everyone present', () => {
    expect(allPresent(rows).every((r) => r.attendanceStatus === 'Present')).toBe(true);
  });

  it('change one student and leave the rest', () => {
    const changed = withMark(rows, 2, 'Present');
    expect(changed[0].attendanceStatus).toBe('Absent');
    expect(changed[1].attendanceStatus).toBe('Present');
  });

  it('edit an open day, or a locked one only with unlock', () => {
    expect(mayEdit(false, false, true)).toBe(true);
    expect(mayEdit(true, false, true)).toBe(false);
    expect(mayEdit(true, true, true)).toBe(true);
    expect(mayEdit(false, true, false)).toBe(false);
  });

  it('give every mark its own key', () => {
    const keys = ATTENDANCE_MARKS.map((m) => m.key);
    expect(new Set(keys).size).toBe(keys.length);
  });
});
