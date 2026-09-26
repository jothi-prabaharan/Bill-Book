/** One level of a document's approval chain, as the owning service reports it (TK-100). */
export interface ApprovalStepView {
  sequence: number;
  label: string;
  status: 'Waiting' | 'Pending' | 'Approved' | 'Rejected' | 'SentBack' | 'Skipped' | 'Escalated' | 'Cancelled';
  approverUserId: string | null;
  roleId: number | null;
  actedByUserId: string | null;
  actedAt: string | null;
  comments: string | null;
}

export interface ApprovalChainView {
  /** Null when no workflow has been involved: the ordinary approve action applies. */
  approvalStatus: 'Draft' | 'InApproval' | 'Approved' | 'Rejected' | null;
  steps: ApprovalStepView[];
  canAct: boolean;
}

export type ApprovalActionName = 'Approve' | 'Reject' | 'SendBack';

/** Whether a document can be sent up for approval: a draft not already waiting. */
export function canSubmitForApproval(documentStatus: string, chain: ApprovalChainView | null): boolean {
  return documentStatus === 'Draft' && chain?.approvalStatus !== 'InApproval';
}

/** How a step's status reads beside its level. */
export function stepStatusLabel(status: ApprovalStepView['status']): string {
  switch (status) {
    case 'Pending':
      return 'Waiting for approval';
    case 'Waiting':
      return 'Not reached yet';
    case 'SentBack':
      return 'Sent back';
    default:
      return status;
  }
}
