import { ApiDocumentLine } from './document-line-scale';

/**
 * Billing a project on an invoice (TK-106): what the project has left to bill,
 * as Accounting and Purchase list it, and the invoice lines chosen items
 * become. The items travel with the draft and are claimed when it posts.
 */

export type ProjectItemKind = 'TimeEntry' | 'Expense' | 'Milestone' | 'BillLine';

export interface ProjectItemRef {
  kind: ProjectItemKind;
  itemId: number;
  projectId?: number | null;
}

export interface UnbilledTimeItem {
  timeEntryId: number;
  workDate: string;
  userId: string;
  projectTaskId: number;
  taskName: string;
  hours: number;
  notes: string | null;
  rate: number;
  amount: number;
}

export interface UnbilledExpenseItem {
  spendMoneyDetailId: number;
  transactionDate: string;
  transactionNo: string | null;
  memo: string | null;
  amount: number;
  markupPercent: number | null;
  billableAmount: number;
}

export interface UnbilledMilestoneItem {
  projectMilestoneId: number;
  name: string;
  amount: number;
  dueDate: string | null;
}

export interface BillableBillLine {
  billDetailId: number;
  billId: number;
  documentNo: string;
  documentDate: string;
  description: string | null;
  amount: number;
  markupPercent: number | null;
  billableAmount: number;
}

/** Accounting's `GET api/projects/{id}/unbilled`. */
export interface ProjectUnbilled {
  projectId: number;
  contactId: number | null;
  billingMethod: string;
  time: UnbilledTimeItem[];
  expenses: UnbilledExpenseItem[];
  milestones: UnbilledMilestoneItem[];
}

/** What the user ticked in "Add project items". */
export interface ProjectBillingPick {
  time: UnbilledTimeItem[];
  expenses: UnbilledExpenseItem[];
  bills: BillableBillLine[];
  milestones: UnbilledMilestoneItem[];
}

/**
 * The invoice lines a pick becomes, and the items they bill. Hours are one
 * line per task and rate; each expense, bill line and milestone is a line of
 * its own at what it is charged on at (markup included). Every line names the
 * project and the income account chosen, and carries no item: a service line.
 */
export function toBillingLines(
  projectId: number,
  pick: ProjectBillingPick,
  accountId: number | null,
): { lines: ApiDocumentLine[]; items: ProjectItemRef[] } {
  const lines: ApiDocumentLine[] = [];
  const items: ProjectItemRef[] = [];
  const line = (description: string, quantity: number, unitPrice: number): ApiDocumentLine => ({
    itemId: null,
    description,
    quantity,
    conversionFactor: 1,
    unitPrice,
    accountId,
    projectId,
  });

  const byTask = new Map<string, UnbilledTimeItem[]>();
  for (const entry of pick.time) {
    const key = `${entry.projectTaskId}:${entry.rate}`;
    byTask.set(key, [...(byTask.get(key) ?? []), entry]);
  }

  for (const entries of byTask.values()) {
    const hours = round2(entries.reduce((sum, e) => sum + e.hours, 0));
    lines.push(line(`${entries[0].taskName} — ${hours} h`, hours, entries[0].rate));
    items.push(...entries.map((e) => ({ kind: 'TimeEntry' as const, itemId: e.timeEntryId, projectId })));
  }

  for (const expense of pick.expenses) {
    lines.push(line(`Expense: ${expense.memo ?? expense.transactionNo ?? 'reimbursable'}`, 1, expense.billableAmount));
    items.push({ kind: 'Expense', itemId: expense.spendMoneyDetailId, projectId });
  }

  for (const bill of pick.bills) {
    lines.push(line(`Re-billed: ${bill.description ?? bill.documentNo}`, 1, bill.billableAmount));
    items.push({ kind: 'BillLine', itemId: bill.billDetailId, projectId });
  }

  for (const milestone of pick.milestones) {
    lines.push(line(`Milestone: ${milestone.name}`, 1, milestone.amount));
    items.push({ kind: 'Milestone', itemId: milestone.projectMilestoneId, projectId });
  }

  return { lines, items };
}

/** Adds items to those already on the invoice, once each. */
export function mergeProjectItems(current: readonly ProjectItemRef[], added: readonly ProjectItemRef[]): ProjectItemRef[] {
  const seen = new Set(current.map((i) => `${i.kind}:${i.itemId}`));
  return [...current, ...added.filter((i) => !seen.has(`${i.kind}:${i.itemId}`))];
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}
