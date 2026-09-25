import { describe, expect, it } from 'vitest';
import { guardianProblem, markProblem, nextExamStatuses, studentProblem } from './student-rules';
import { SaveStudent } from './student.models';

const valid = (): SaveStudent => ({
  firstName: 'Arun',
  lastName: 'Raman',
  dateOfBirth: '2015-02-10',
  gender: 'Male',
  admissionDate: '2026-06-01',
  studentStatus: 'Active',
  leavingDate: null,
  bloodGroup: null,
  nationalId: null,
  guardians: [{ contactId: 1, relationship: 'Mother', isPrimary: true, hasPortalAccess: true }],
});

describe('student rules (TK-61)', () => {
  it('pass a valid student', () => {
    expect(studentProblem(valid())).toBeNull();
  });

  it('refuse a birth on or after admission', () => {
    expect(studentProblem({ ...valid(), dateOfBirth: '2026-06-01' })).not.toBeNull();
  });

  it('need a leaving date for a student who has left', () => {
    expect(studentProblem({ ...valid(), studentStatus: 'Alumni' })).not.toBeNull();
    expect(studentProblem({ ...valid(), studentStatus: 'Alumni', leavingDate: '2027-03-31' })).toBeNull();
  });

  it('need one or two guardians with exactly one primary', () => {
    expect(guardianProblem([])).not.toBeNull();
    expect(
      guardianProblem([
        { contactId: 1, relationship: 'Mother', isPrimary: false, hasPortalAccess: false },
        { contactId: 2, relationship: 'Father', isPrimary: false, hasPortalAccess: false },
      ]),
    ).not.toBeNull();
    expect(
      guardianProblem([
        { contactId: 1, relationship: 'Mother', isPrimary: true, hasPortalAccess: false },
        { contactId: 1, relationship: 'Father', isPrimary: false, hasPortalAccess: false },
      ]),
    ).not.toBeNull();
  });

  it('move an exam only as the server allows', () => {
    expect(nextExamStatuses('Planned')).toEqual(['MarksOpen']);
    expect(nextExamStatuses('Published')).toEqual(['MarksOpen', 'Locked']);
    expect(nextExamStatuses('Locked')).toEqual([]);
  });

  it('take a mark from zero to the maximum, or absent', () => {
    expect(markProblem({ enrolmentId: 1, rollNo: 1, studentName: 'A', marks: 101, isAbsent: false }, 100)).not.toBeNull();
    expect(markProblem({ enrolmentId: 1, rollNo: 1, studentName: 'A', marks: null, isAbsent: true }, 100)).toBeNull();
    expect(markProblem({ enrolmentId: 1, rollNo: 1, studentName: 'A', marks: 0, isAbsent: false }, 100)).toBeNull();
  });
});
