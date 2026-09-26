/**
 * Settings › Approval workflows (TK-103): the shapes Master's
 * `api/approval-workflows` speaks, and the catalogue of what a workflow can
 * govern. Master reads enums as numbers, so kinds travel as their numbers.
 */

/** What a workflow governs. The numbers are `Shared.Kernel.Approvals.ApprovalRequestKind`. */
export interface RequestKindOption {
  value: number;
  label: string;
  group: 'RetailErp' | 'HRMS & Payroll';
}

export const REQUEST_KINDS: readonly RequestKindOption[] = [
  { value: 101, label: 'Purchase order', group: 'RetailErp' },
  { value: 102, label: 'Purchase bill', group: 'RetailErp' },
  { value: 103, label: 'Debit note', group: 'RetailErp' },
  { value: 104, label: 'Spend money', group: 'RetailErp' },
  { value: 105, label: 'Manual journal', group: 'RetailErp' },
  { value: 106, label: 'Credit note', group: 'RetailErp' },
  { value: 107, label: 'Sales discount override', group: 'RetailErp' },
  { value: 108, label: 'Credit limit override', group: 'RetailErp' },
  { value: 109, label: 'Stock adjustment', group: 'RetailErp' },
  { value: 1, label: 'Leave', group: 'HRMS & Payroll' },
  { value: 2, label: 'Attendance regularisation', group: 'HRMS & Payroll' },
  { value: 3, label: 'Overtime', group: 'HRMS & Payroll' },
  { value: 4, label: 'Compensatory off', group: 'HRMS & Payroll' },
  { value: 5, label: 'Leave encashment', group: 'HRMS & Payroll' },
  { value: 6, label: 'Appraisal', group: 'HRMS & Payroll' },
  { value: 7, label: 'Salary revision', group: 'HRMS & Payroll' },
  { value: 8, label: 'Loan', group: 'HRMS & Payroll' },
  { value: 9, label: 'Job requisition', group: 'HRMS & Payroll' },
  { value: 10, label: 'Offer', group: 'HRMS & Payroll' },
  { value: 11, label: 'Claim', group: 'HRMS & Payroll' },
  { value: 12, label: 'Separation', group: 'HRMS & Payroll' },
  { value: 13, label: 'Full and final settlement', group: 'HRMS & Payroll' },
];

/** How a level's approver is found. The numbers are `ApproverKind`. */
export const APPROVER_KINDS = {
  reportingChain: 1,
  relationship: 2,
  departmentHead: 3,
  roleHolder: 4,
  namedEmployee: 5,
  namedUser: 6,
} as const;

const APPROVER_KIND_LABELS: Record<number, string> = {
  1: 'Reporting manager',
  2: 'Relationship',
  3: 'Department head',
  4: 'Anyone holding a role',
  5: 'A named employee',
  6: 'A named user',
};

export function requestKindLabel(kind: number): string {
  return REQUEST_KINDS.find((k) => k.value === kind)?.label ?? `Kind ${kind}`;
}

/**
 * The ways a level may find its approver for a kind of request. A RetailErp
 * document has no employee behind it, so only a role or a named user can
 * approve it; an HRMS request can use every way.
 */
export function approverKindsFor(requestKind: number): { value: number; label: string }[] {
  const retail = requestKind >= 100;
  const kinds = retail
    ? [APPROVER_KINDS.roleHolder, APPROVER_KINDS.namedUser]
    : [1, 2, 3, 4, 5, 6];
  return kinds.map((value) => ({ value, label: APPROVER_KIND_LABELS[value] }));
}

export interface ApprovalLevel {
  label: string;
  approverKind: number;
  reportingDepth: number | null;
  relationshipTypeId: number | null;
  roleId: number | null;
  employeeId: number | null;
  userId: string | null;
  aboveAmount: number | null;
  isOptional: boolean;
  canEdit: boolean;
  isCommentRequired: boolean;
  escalateAfterDays: number | null;
}

export interface SaveApprovalWorkflow {
  name: string;
  requestKind: number;
  departmentId: number | null;
  gradeId: number | null;
  workLocationId: number | null;
  effectiveFrom: string;
  isActive: boolean;
  levels: ApprovalLevel[];
}

export interface ApprovalWorkflowView extends SaveApprovalWorkflow {
  approvalWorkflowId: number;
  app: string;
}

export function blankLevel(requestKind: number): ApprovalLevel {
  return {
    label: '',
    approverKind: requestKind >= 100 ? APPROVER_KINDS.roleHolder : APPROVER_KINDS.reportingChain,
    reportingDepth: requestKind >= 100 ? null : 1,
    relationshipTypeId: null,
    roleId: null,
    employeeId: null,
    userId: null,
    aboveAmount: null,
    isOptional: false,
    canEdit: false,
    isCommentRequired: false,
    escalateAfterDays: null,
  };
}

/** Moves a level from one position to another, as a drag or the arrow buttons do. */
export function moveLevel<T>(levels: readonly T[], from: number, to: number): T[] {
  const next = [...levels];
  if (from < 0 || from >= next.length || to < 0 || to >= next.length || from === to) {
    return next;
  }
  const [moved] = next.splice(from, 1);
  next.splice(to, 0, moved);
  return next;
}

/**
 * What stops a workflow being saved, or null. The server checks the same, but
 * a level with no approver is worth catching before the round trip.
 */
export function workflowProblem(workflow: SaveApprovalWorkflow): string | null {
  if (!workflow.name.trim()) {
    return 'Give the workflow a name.';
  }
  if (workflow.levels.length === 0) {
    return 'Add at least one level.';
  }
  for (const [index, level] of workflow.levels.entries()) {
    const n = index + 1;
    if (!level.label.trim()) {
      return `Level ${n} needs a label.`;
    }
    if (level.approverKind === APPROVER_KINDS.roleHolder && level.roleId === null) {
      return `Level ${n} needs a role.`;
    }
    if (level.approverKind === APPROVER_KINDS.namedUser && !level.userId) {
      return `Level ${n} needs a user.`;
    }
    if (level.approverKind === APPROVER_KINDS.namedEmployee && level.employeeId === null) {
      return `Level ${n} needs an employee.`;
    }
    if (level.approverKind === APPROVER_KINDS.relationship && level.relationshipTypeId === null) {
      return `Level ${n} needs a relationship type.`;
    }
  }
  return null;
}
