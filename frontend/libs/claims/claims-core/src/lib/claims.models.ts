export type ClaimStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Paid';
export type PayoutMode = 'Payroll' | 'Direct';
export type LimitPeriod = 'PerClaim' | 'Monthly' | 'Yearly';
export type ApprovalStatus = 'Draft' | 'InApproval' | 'Approved' | 'Rejected' | 'SentBack';

export interface ClaimCategoryView {
  claimCategoryId: number;
  code: string;
  name: string;
  isReceiptRequired: boolean;
  ledgerAccountId?: number | null;
  isTaxable: boolean;
  isActive: boolean;
}

export interface CreateClaimCategory {
  code: string;
  name: string;
  isReceiptRequired: boolean;
  ledgerAccountId?: number | null;
  isTaxable: boolean;
  isActive: boolean;
}

export interface ClaimLimitView {
  claimLimitId: number;
  claimCategoryId: number;
  categoryName: string;
  gradeId?: number | null;
  limitPeriod: LimitPeriod;
  amount: number;
}

export interface SaveClaimLimit {
  claimCategoryId: number;
  gradeId?: number | null;
  limitPeriod: LimitPeriod;
  amount: number;
}

export interface ExpenseClaimLineView {
  expenseClaimLineId: number;
  expenseClaimId: number;
  claimCategoryId: number;
  categoryCode: string;
  categoryName: string;
  expenseDate: string;
  description: string;
  amount: number;
  receiptAttachmentKey?: string | null;
}

export interface SaveExpenseClaimLine {
  expenseClaimLineId?: number | null;
  claimCategoryId: number;
  expenseDate: string;
  description: string;
  amount: number;
  receiptAttachmentKey?: string | null;
}

export interface ExpenseClaimView {
  expenseClaimId: number;
  claimNo: string;
  employeeId: number;
  employeeCode?: string | null;
  employeeName?: string | null;
  claimDate: string;
  totalAmount: number;
  approvedAmount: number;
  claimStatus: ClaimStatus;
  payoutMode: PayoutMode;
  payrollRunId?: number | null;
  spendMoneyId?: number | null;
  approvalStatus: ApprovalStatus;
  currentStepLabel?: string | null;
  currentApproverEmployeeId?: number | null;
  lines: ExpenseClaimLineView[];
}

export interface CreateExpenseClaim {
  employeeId: number;
  claimDate: string;
  payoutMode: PayoutMode;
  lines: SaveExpenseClaimLine[];
}

export interface UpdateExpenseClaim {
  claimDate: string;
  payoutMode: PayoutMode;
  lines: SaveExpenseClaimLine[];
}

export interface ActClaimApproval {
  action: 'Approve' | 'Reject' | 'SendBack';
  comments?: string;
}

export interface PayoutClaim {
  payoutMode?: PayoutMode;
  bankAccountId?: number | null;
  paymentReference?: string;
  payrollRunId?: number | null;
}

export interface ApplyClaimSelf {
  claimDate: string;
  payoutMode: PayoutMode;
  lines: SaveExpenseClaimLine[];
}
