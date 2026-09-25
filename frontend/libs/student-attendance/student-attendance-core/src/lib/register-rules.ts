import { AttendanceStatus, RegisterRow } from './student-attendance.models';

/** Every row marked present: the start of most mornings (S3, TK-63). */
export function allPresent(rows: readonly RegisterRow[]): RegisterRow[] {
  return rows.map((r) => ({ ...r, attendanceStatus: 'Present' as AttendanceStatus }));
}

/** The rows with one student's mark changed. */
export function withMark(rows: readonly RegisterRow[], enrolmentId: number, status: AttendanceStatus): RegisterRow[] {
  return rows.map((r) => (r.enrolmentId === enrolmentId ? { ...r, attendanceStatus: status } : r));
}

/** Whether the page may change marks: an open day, or a locked one for someone who may unlock. */
export function mayEdit(isLocked: boolean, mayUnlock: boolean, canEdit: boolean): boolean {
  return canEdit && (!isLocked || mayUnlock);
}
