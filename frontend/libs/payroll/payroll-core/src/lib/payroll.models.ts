export type ComponentKind = 'Earning' | 'Deduction' | 'Statutory';
export type SalaryValueType = 'FlatAmount' | 'Formula' | 'Percentage';
export type PayrollRunStatus = 'Draft' | 'Processed' | 'Approved' | 'Posted' | 'MarkedPaid' | 'Reversed';

export interface SalaryComponentView {
  salaryComponentId: number;
  name: string;
  kind: ComponentKind;
  valueType: SalaryValueType;
  isTaxable: boolean;
  formula?: string;
  ledgerAccountId?: number;
}

export interface SaveSalaryComponent {
  name: string;
  kind: ComponentKind;
  valueType: SalaryValueType;
  isTaxable: boolean;
  formula?: string;
  ledgerAccountId?: number;
}

export interface SalaryStructureComponentView {
  salaryStructureComponentId: number;
  salaryComponentId: number;
  componentName: string;
  kind: ComponentKind;
  valueType: SalaryValueType;
  flatAmount?: number;
  percentage?: number;
}

export interface SalaryStructureView {
  salaryStructureId: number;
  name: string;
  components: SalaryStructureComponentView[];
}

export interface SaveSalaryStructure {
  name: string;
  components: {
    salaryComponentId: number;
    valueType: SalaryValueType;
    flatAmount?: number;
    percentage?: number;
  }[];
}

export interface PayrollRunView {
  payrollRunId: number;
  month: string;
  status: PayrollRunStatus;
  employeeCount: number;
  totalGrossEarnings: number;
  totalGrossDeductions: number;
  totalNetPay: number;
  daysSource: string;
  journalId?: number;
}

export interface PayslipLineView {
  payslipLineId: number;
  salaryComponentId: number;
  componentName: string;
  kind: ComponentKind;
  amount: number;
}

export interface PayslipView {
  payslipId: number;
  payrollRunId: number;
  employeeId: number;
  paidDays: number;
  grossEarnings: number;
  grossDeductions: number;
  netPay: number;
  lines: PayslipLineView[];
}

export interface MyPayslipSummary {
  payslipId: number;
  payrollRunId: number;
  month: string;
  monthName: string;
  paidDays: number;
  grossEarnings: number;
  grossDeductions: number;
  netPay: number;
  status: string;
}

