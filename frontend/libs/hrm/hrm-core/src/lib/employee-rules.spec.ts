import { describe, expect, it } from 'vitest';
import { blankEmployee, employeeProblem, fullName, isMasked, nomineeShareProblems } from './employee-rules';

/** The employee form's own checks, the server's rules seen early (TK-48). */
describe('employee rules', () => {
  const valid = () => ({
    ...blankEmployee('2026-04-01'),
    firstName: 'Priya',
    dateOfBirth: '1994-05-12',
    phone: '9840012345',
    departmentId: 1,
    designationId: 1,
    gradeId: 1,
    workLocationId: 1,
  });

  it('joins the parts of a name that are there', () => {
    expect(fullName('Priya', null, 'Raman')).toBe('Priya Raman');
    expect(fullName('Priya', ' ', '')).toBe('Priya');
  });

  it('finds the nomination kinds whose shares do not make 100', () => {
    expect(
      nomineeShareProblems([
        { familyMemberIndex: 0, nominationKind: 'Pf', sharePercent: 60 },
        { familyMemberIndex: 1, nominationKind: 'Pf', sharePercent: 40 },
        { familyMemberIndex: 0, nominationKind: 'Gratuity', sharePercent: 90 },
      ]),
    ).toEqual(['Gratuity']);
  });

  it('knows a masked number from a real one', () => {
    expect(isMasked('XXXXXX234F')).toBe(true);
    expect(isMasked('ABCDE1234F')).toBe(false);
  });

  it('passes a valid form and a masked PAN sent back', () => {
    expect(employeeProblem(valid())).toBeNull();
    expect(employeeProblem({ ...valid(), pan: 'XXXXXX234F' })).toBeNull();
  });

  it('names the first problem', () => {
    expect(employeeProblem({ ...valid(), firstName: '' })).toBe('Give the first name.');
    expect(employeeProblem({ ...valid(), pan: 'ABC' })).toMatch(/PAN/);
    expect(employeeProblem({ ...valid(), employeeStatus: 'Exited' })).toMatch(/exit date/);
    expect(
      employeeProblem({
        ...valid(),
        bankDetails: [{ accountHolder: 'P', accountNo: '1', ifsc: 'HDFC0001234', bankName: 'HDFC', isPrimary: false }],
      }),
    ).toMatch(/exactly one/);
  });
});
