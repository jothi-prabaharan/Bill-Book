import { ExamStatus, MarkRow, SaveStudent, StudentGuardian } from './student.models';

/**
 * The student and exam rules the screens check before sending (S1, TK-61).
 * The server checks the same, and its answer wins; these only save a round trip.
 */

export const MAX_GUARDIANS = 2;

/** One or two guardians, none twice, exactly one primary. Null when fine. */
export function guardianProblem(guardians: readonly StudentGuardian[]): string | null {
  if (guardians.length === 0 || guardians.length > MAX_GUARDIANS) {
    return 'A student needs one or two guardians.';
  }

  if (new Set(guardians.map((g) => g.contactId)).size !== guardians.length) {
    return 'The same guardian is listed twice.';
  }

  return guardians.filter((g) => g.isPrimary).length === 1
    ? null
    : 'Mark exactly one guardian as primary. The primary guardian receives the fee demands.';
}

/** The student's own rules. Dates are ISO `yyyy-MM-dd`, which compare as text. */
export function studentProblem(student: SaveStudent): string | null {
  if (!student.firstName?.trim()) {
    return 'Give the first name.';
  }

  if (!student.dateOfBirth || !student.admissionDate || student.dateOfBirth >= student.admissionDate) {
    return 'The date of birth must be before the admission date.';
  }

  if (student.studentStatus !== 'Active' && !student.leavingDate) {
    return 'A student who has left needs a leaving date.';
  }

  return guardianProblem(student.guardians);
}

/** The moves an exam may make, as the server allows them. */
export function nextExamStatuses(status: ExamStatus): ExamStatus[] {
  switch (status) {
    case 'Planned':
      return ['MarksOpen'];
    case 'MarksOpen':
      return ['Published'];
    case 'Published':
      return ['MarksOpen', 'Locked'];
    default:
      return [];
  }
}

/** A mark is absent, or between zero and the maximum. */
export function markProblem(row: MarkRow, maxMarks: number): string | null {
  if (row.isAbsent) {
    return null;
  }

  return row.marks !== null && row.marks >= 0 && row.marks <= maxMarks
    ? null
    : `Enter marks from 0 to ${maxMarks}, or mark the student absent.`;
}

export const EXAM_STATUS_LABELS: Readonly<Record<ExamStatus, string>> = {
  Planned: 'Planned',
  MarksOpen: 'Open for marks',
  Published: 'Published',
  Locked: 'Locked',
};
