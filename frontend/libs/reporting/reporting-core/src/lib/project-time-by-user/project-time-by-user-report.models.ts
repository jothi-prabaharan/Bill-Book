import { ProjectLookupItem } from '../project-profitability/project-profitability-report.models';

export interface ProjectTimeByUserReportRequest {
  fromDate: string;
  toDate: string;
  userId?: string | null;
  projectId?: number | null;
  groupBy?: 'user' | 'project';
  orgId?: string | null;
}

export interface ProjectTimeItem {
  projectCode: string;
  projectName: string;
  taskName: string;
  userId: string;
  userName: string;
  hours: number;
  billableHours: number;
  billedHours: number;
  nonBillableHours: number;
  utilisationPercent?: number | null;
}

export interface ProjectTimeGroup {
  groupKey: string;
  groupName: string;
  groupSubtext?: string | null;
  totalHours: number;
  totalBillableHours: number;
  totalBilledHours: number;
  totalNonBillableHours: number;
  totalUtilisationPercent?: number | null;
  items: ProjectTimeItem[];
}

export interface ProjectTimeSummary {
  totalUsers: number;
  totalProjects: number;
  totalHours: number;
  totalBillableHours: number;
  totalBilledHours: number;
  totalNonBillableHours: number;
  overallUtilisationPercent?: number | null;
}

export interface ProjectTimeByUserReportResponse {
  fromDate: string;
  toDate: string;
  groupBy: string;
  summary: ProjectTimeSummary;
  groups: ProjectTimeGroup[];
}

export interface ProjectUserLookupItem {
  userId: string;
  name: string;
}

export interface ProjectTimeByUserLookupsResponse {
  users: ProjectUserLookupItem[];
  projects: ProjectLookupItem[];
}
