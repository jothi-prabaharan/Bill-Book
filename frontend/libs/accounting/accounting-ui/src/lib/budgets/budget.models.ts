/** A budget as `GET api/budgets` lists it (TK-108). */
export interface BudgetListItem {
  budgetId: number;
  name: string;
  financialYearStart: string;
  status: 'Draft' | 'Approved' | 'Superseded';
  isBranchDefault: boolean;
  byProject: boolean;
  revisionOf: number | null;
  revisionReason: string | null;
  approvedAt: string | null;
  total: number;
}

/** One account's (and project's) twelve months. */
export interface BudgetLineView {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  projectId: number | null;
  projectCode: string | null;
  amounts: number[];
  total: number;
}

export interface BudgetView extends BudgetListItem {
  lines: BudgetLineView[];
}

/** An account as `GET api/accounts` lists it, the fields the grid reads. */
export interface BudgetAccount {
  accountId: number;
  accountTypeId: number;
  accountCode: string;
  accountName: string;
  parentAccountId: number | null;
  isActive: boolean;
}
