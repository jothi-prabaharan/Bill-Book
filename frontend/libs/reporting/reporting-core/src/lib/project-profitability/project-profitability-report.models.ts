export interface ProjectProfitabilityReportRequest {
  from: string;
  to: string;
  customerId?: number | null;
  status?: string | null;
  billingMethod?: string | null;
  projectId?: number | null;
  orgId?: string | null;
}

export interface ProjectProfitabilityRow {
  projectId: number;
  projectCode: string;
  projectName: string;
  customerId?: number | null;
  customer: string;
  status: string;
  billingMethod: string;
  revenue: number;
  cost: number;
  profit: number;
  marginPercent?: number | null;
  budgetAmount?: number | null;
  budgetVariance?: number | null;
}

export interface ProjectProfitabilitySummary {
  totalProjects: number;
  totalRevenue: number;
  totalCost: number;
  totalProfit: number;
  overallMarginPercent?: number | null;
  totalBudgetAmount: number;
}

export interface ProjectProfitabilityReportResponse {
  from: string;
  to: string;
  rows: ProjectProfitabilityRow[];
  summary: ProjectProfitabilitySummary;
}

export interface ProjectLookupItem {
  id: number;
  code: string;
  name: string;
}

export interface ProjectProfitabilityLookups {
  customers: ProjectLookupItem[];
  statuses: string[];
  billingMethods: string[];
  projects: ProjectLookupItem[];
}
