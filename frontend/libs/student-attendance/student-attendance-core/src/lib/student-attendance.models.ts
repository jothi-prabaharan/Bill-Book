import { RegisterStatus } from '@bill-book/ui-components';

/** The student attendance service's shapes (S3, TK-63), as the API sends them. */

export type AttendanceStatus = 'Present' | 'Absent' | 'Late' | 'HalfDay' | 'Leave' | 'Holiday';

export interface RegisterRow {
  enrolmentId: number;
  rollNo: number | null;
  admissionNo: string | null;
  studentName: string | null;
  attendanceStatus: AttendanceStatus;
  remarks: string | null;
}

export interface RegisterView {
  sectionId: number;
  attendanceDate: string;
  isLocked: boolean;
  isTaken: boolean;
  rows: RegisterRow[];
}

/** The marks, in the order a tap cycles through them, with the key that sets each. */
export const ATTENDANCE_MARKS: readonly RegisterStatus<AttendanceStatus>[] = [
  { value: 'Present', label: 'Present', key: 'P', glyph: 'P', tone: 'good' },
  { value: 'Absent', label: 'Absent', key: 'A', glyph: 'A', tone: 'bad' },
  { value: 'Late', label: 'Late', key: 'L', glyph: 'L', tone: 'warn' },
  { value: 'HalfDay', label: 'Half day', key: 'H', glyph: '½', tone: 'warn' },
  { value: 'Leave', label: 'Leave', key: 'V', glyph: 'V', tone: 'neutral' },
  { value: 'Holiday', label: 'Holiday', key: 'O', glyph: 'O', tone: 'neutral' },
];
