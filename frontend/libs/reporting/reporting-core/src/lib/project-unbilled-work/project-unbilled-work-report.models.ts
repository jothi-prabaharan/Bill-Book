import { ProjectLookupItem } from '../project-profitability/project-profitability-report.models';

export interface ProjectUnbilledWorkReportRequest {
  asOfDate: string;
  customerId?: number | null;
  projectId?: number | null;
  orgId?: string | null;
}

export interface ProjectUnbilledLine {
  lineType: string;
  id: number;
  date?: string | null;
  description: string;
  reference?: string | null;
  quantity: number;
  unitRate: number;
  markupPercent?: number | null;
  amount: number;
}

export interface ProjectUnbilledProject {
  projectId: number;
  projectCode: string;
  projectName: string;
  customerId?: number | null;
  customer: string;
  billingMethod: string;
  rateBasis?: string | null;
  hourlyRate?: number | null;
  unbilledHours: number;
  timeValue: number;
  expenses: number;
  milestones: number;
  total: number;
  lineCount: number;
  lines: ProjectUnbilledLine[];
}

export interface ProjectUnbilledSummary {
  totalProjects: number;
  totalUnbilledHours: number;
  totalTimeValue: number;
  totalExpenses: number;
  totalMilestones: number;
  grandTotal: number;
}

export interface ProjectUnbilledWorkReportResponse {
  asOfDate: string;
  summary: ProjectUnbilledSummary;
  projects: ProjectUnbilledProject[];
}

export interface ProjectUnbilledLookupsResponse {
  customers: ProjectLookupItem[];
  projects: ProjectLookupItem[];
}
