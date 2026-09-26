import { describe, expect, it } from 'vitest';
import {
  APPROVER_KINDS,
  approverKindsFor,
  blankLevel,
  moveLevel,
  requestKindLabel,
  workflowProblem,
  SaveApprovalWorkflow,
} from './approval-workflows.model';

const workflow = (overrides: Partial<SaveApprovalWorkflow> = {}): SaveApprovalWorkflow => ({
  name: 'Purchase orders',
  requestKind: 101,
  departmentId: null,
  gradeId: null,
  workLocationId: null,
  effectiveFrom: '2026-09-26',
  isActive: true,
  levels: [{ ...blankLevel(101), label: 'Accountant', roleId: 3 }],
  ...overrides,
});

describe('approval workflows model', () => {
  it('offers a RetailErp document only a role or a named user', () => {
    expect(approverKindsFor(101).map((k) => k.value)).toEqual([APPROVER_KINDS.roleHolder, APPROVER_KINDS.namedUser]);
    expect(approverKindsFor(1)).toHaveLength(6);
  });

  it('names every kind, and an unknown one by its number', () => {
    expect(requestKindLabel(108)).toBe('Credit limit override');
    expect(requestKindLabel(999)).toBe('Kind 999');
  });

  it('moves a level and ignores a move out of range', () => {
    expect(moveLevel(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b']);
    expect(moveLevel(['a', 'b'], 0, 5)).toEqual(['a', 'b']);
  });

  it('finds what stops a save', () => {
    expect(workflowProblem(workflow())).toBeNull();
    expect(workflowProblem(workflow({ name: ' ' }))).toBe('Give the workflow a name.');
    expect(workflowProblem(workflow({ levels: [] }))).toBe('Add at least one level.');
    expect(workflowProblem(workflow({ levels: [{ ...blankLevel(101), label: 'Owner' }] }))).toBe('Level 1 needs a role.');
    expect(
      workflowProblem(workflow({ levels: [{ ...blankLevel(101), label: 'Owner', approverKind: APPROVER_KINDS.namedUser }] })),
    ).toBe('Level 1 needs a user.');
  });
});
