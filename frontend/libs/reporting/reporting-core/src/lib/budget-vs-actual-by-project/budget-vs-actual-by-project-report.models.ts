import {
  BudgetPeriod,
  BudgetVsActualBudget,
  BudgetVsActualProfit,
  BudgetVsActualSectionResult,
} from '../budget-vs-actual/budget-vs-actual-report.models';

/**
 * Budget vs Actual by Project's contract (TK-207), field for field with
 * `BudgetVsActualByProjectReportModels.cs`. Each project is laid out as
 * Budget vs Actual lays out the branch, so its sections and profit lines are
 * Budget vs Actual's own types. Money is in the branch's base currency.
 */

export type ProjectStatus = 'Active' | 'OnHold' | 'Completed' | 'Cancelled';

/** What the report is asked for. Budget, date and period are required; no project means every project. */
export interface BudgetVsActualByProjectReportRequest {
  budgetId: number;
  asOfDate: string;
  period: BudgetPeriod;
  projectId?: number | null;
}

/** A project the report can be narrowed to. */
export interface BudgetVsActualProjectOption {
  projectId: number;
  projectCode: string;
  projectName: string;
  status: ProjectStatus;
}

/** One project's budget against its postings. */
export interface BudgetVsActualProjectResult {
  projectId: number;
  projectCode: string;
  projectName: string;
  status: ProjectStatus;
  sections: BudgetVsActualSectionResult[];
  grossProfit: BudgetVsActualProfit;
  netProfit: BudgetVsActualProfit;
}

export interface BudgetVsActualByProjectReportResult {
  budget: BudgetVsActualBudget;
  asOfDate: string;
  period: BudgetPeriod;
  periodFrom: number;
  periodTo: number;
  fromDate: string;
  toDate: string;
  projectId: number | null;
  /** Every project with a budget or an actual in the months covered, by code. */
  projects: BudgetVsActualProjectResult[];
  /** Across the projects shown. */
  grossProfit: BudgetVsActualProfit;
  netProfit: BudgetVsActualProfit;
}

const STATUS_WORDS: Record<ProjectStatus, string> = {
  Active: 'active',
  OnHold: 'on hold',
  Completed: 'completed',
  Cancelled: 'cancelled',
};

/** A project as the page names it: its code and name, and its status unless active. */
export function projectLabel(project: Pick<BudgetVsActualProjectOption, 'projectCode' | 'projectName' | 'status'>): string {
  const name = project.projectCode ? `${project.projectCode} · ${project.projectName}` : project.projectName;
  return project.status === 'Active' ? name : `${name} (${STATUS_WORDS[project.status]})`;
}

/** Whether a project shows gross profit: only when it has revenue or a cost of sales to take it from. */
export function hasGrossProfit(project: Pick<BudgetVsActualProjectResult, 'sections'>): boolean {
  return project.sections.some((s) => (s.section === 'Revenue' || s.section === 'CostOfSales') && s.lines.length > 0);
}
