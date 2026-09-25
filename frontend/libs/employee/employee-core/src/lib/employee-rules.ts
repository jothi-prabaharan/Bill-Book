import { EmployeeNominee, NominationKind, SaveEmployee } from './employee.models';

/**
 * The employee rules the form can check before it saves (TK-48). The server
 * checks the same rules and is the authority; these only let the form say what
 * is wrong next to the field instead of after a round trip.
 */

/** "Priya Raman", skipping the parts that are blank. */
export function fullName(first: string, middle?: string | null, last?: string | null): string {
  return [first, middle, last].filter((p) => p && p.trim().length > 0).join(' ');
}

/** The nomination kinds whose shares do not add up to 100. */
export function nomineeShareProblems(nominees: readonly EmployeeNominee[]): NominationKind[] {
  const totals = new Map<NominationKind, number>();
  for (const n of nominees) {
    totals.set(n.nominationKind, (totals.get(n.nominationKind) ?? 0) + n.sharePercent);
  }
  return [...totals.entries()].filter(([, total]) => Math.abs(total - 100) > 0.0001).map(([kind]) => kind);
}

/** Whether a value is what the server sends in place of a hidden number. */
export function isMasked(value: string | null | undefined): boolean {
  return !!value && /^X+.{1,4}$/.test(value);
}

/** The first problem with a form, in the order a person would fix it, or null. */
export function employeeProblem(e: SaveEmployee): string | null {
  if (!e.firstName?.trim()) return 'Give the first name.';
  if (!e.dateOfBirth) return 'Give the date of birth.';
  if (!e.joiningDate) return 'Give the joining date.';
  if (!e.phone?.trim()) return 'Give a phone number.';
  if (e.employeeStatus === 'Exited' && !e.exitDate) return 'Give the exit date for an employee who has left.';
  if (e.pan && !isMasked(e.pan) && !/^[A-Z]{5}[0-9]{4}[A-Z]$/.test(e.pan.trim().toUpperCase())) {
    return 'A PAN is five letters, four digits and a letter.';
  }
  if (e.aadhaar && !isMasked(e.aadhaar) && !/^[0-9]{12}$/.test(e.aadhaar.replace(/\s/g, ''))) {
    return 'An Aadhaar is 12 digits.';
  }
  if (e.bankDetails.length > 0 && e.bankDetails.filter((b) => b.isPrimary).length !== 1) {
    return 'Mark exactly one bank account as the one salary is paid into.';
  }
  if (e.nominees.some((n) => n.familyMemberIndex < 0 || n.familyMemberIndex >= e.familyMembers.length)) {
    return 'Each nominee must be one of the family members.';
  }
  if (nomineeShareProblems(e.nominees).length > 0) {
    return 'The nominee shares for each kind of nomination must add up to 100 percent.';
  }
  return null;
}

/** A blank employee for the new-employee form. */
export function blankEmployee(today: string): SaveEmployee {
  return {
    firstName: '',
    dateOfBirth: '',
    gender: 'NotStated',
    maritalStatus: 'NotStated',
    departmentId: 0,
    designationId: 0,
    gradeId: 0,
    workLocationId: 0,
    joiningDate: today,
    employmentType: 'Permanent',
    employeeStatus: 'Active',
    phone: '',
    isPfApplicable: true,
    isEsiApplicable: false,
    isPtApplicable: true,
    isLwfApplicable: false,
    addresses: [],
    contacts: [],
    familyMembers: [],
    nominees: [],
    education: [],
    previousEmployments: [],
    bankDetails: [],
    documents: [],
    assetIssues: [],
  };
}
