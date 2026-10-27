import { ProjectLookupItem } from '../project-profitability/project-profitability-report.models';

export interface ProjectBudgetVsActualReportRequest {
  asOfDate: string;
  status?: string | null;
  customerId?: number | null;
  projectId?: number | null;
  orgId?: string | null;
}

export interface ProjectTaskBudget {
  projectTaskId: number;
  taskName: string;
  budgetHours?: number | null;
  actualHours: number;
  hoursVariance?: number | null;
  hoursUsedPercent?: number | null;
  isOverBudget: boolean;
}

export interface ProjectBudgetVsActualProject {
  projectId: number;
  projectCode: string;
  projectName: string;
  customerId?: number | null;
  customer: string;
  status: string;
  budgetAmount?: number | null;
  actualCost: number;
  costVariance?: number | null;
  costUsedPercent?: number | null;
  isCostOverBudget: boolean;
  budgetHours?: number | null;
  actualHours: number;
  hoursVariance?: number | null;
  hoursUsedPercent?: number | null;
  isHoursOverBudget: boolean;
  tasks: ProjectTaskBudget[];
}

export interface ProjectBudgetVsActualSummary {
  totalProjects: number;
  totalBudgetAmount: number;
  totalActualCost: number;
  totalCostVariance: number;
  totalCostUsedPercent?: number | null;
  totalBudgetHours: number;
  totalActualHours: number;
  totalHoursVariance: number;
  totalHoursUsedPercent?: number | null;
  overBudgetProjectsCount: number;
}

export interface ProjectBudgetVsActualReportResponse {
  asOfDate: string;
  projects: ProjectBudgetVsActualProject[];
  summary: ProjectBudgetVsActualSummary;
}

export interface ProjectBudgetLookupsResponse {
  customers: ProjectLookupItem[];
  statuses: string[];
  projects: ProjectLookupItem[];
}
