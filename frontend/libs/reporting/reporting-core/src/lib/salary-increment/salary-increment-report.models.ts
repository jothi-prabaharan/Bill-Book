/**
 * The Salary Increment Report's contract (TK-198), field for field with
 * `SalaryIncrementReportModels.cs`. A salary is the annual CTC Payroll stores
 * on each revision.
 */

/** What the report is asked for. Dates are ISO `yyyy-MM-dd`. */
export interface SalaryIncrementReportRequest {
  fromDate: string;
  toDate: string;
  employeeId?: number | null;
  departmentId?: number | null;
  /** The branch, when named; it must be the token's own, or the server answers 403. */
  orgId?: string | null;
}

export interface SalaryIncrementReportRow {
  salaryRevisionId: number;
  employeeId: number;
  employeeCode: string;
  employeeName: string;
  department: string | null;
  designation: string | null;
  effectiveDate: string;
  previousSalary: number;
  incrementAmount: number;
  /** Null when the previous salary was zero. */
  incrementPercent: number | null;
  revisedSalary: number;
  /** Who recorded the revision; salary revisions are not approved in the product. */
  recordedBy: string | null;
  remarks: string | null;
}

export interface SalaryIncrementReportTotals {
  revisionCount: number;
  employeeCount: number;
  totalPreviousSalary: number;
  totalIncrementAmount: number;
  totalRevisedSalary: number;
  /** Null when no row has a percentage. */
  averageIncrementPercent: number | null;
}

export interface SalaryIncrementReportResult {
  fromDate: string;
  toDate: string;
  rows: SalaryIncrementReportRow[];
  totals: SalaryIncrementReportTotals;
}

export interface SalaryIncrementDepartmentOption {
  departmentId: number;
  name: string;
}

export interface SalaryIncrementEmployeeOption {
  employeeId: number;
  employeeCode: string;
  name: string;
  departmentId: number;
}

export interface SalaryIncrementReportOptions {
  departments: SalaryIncrementDepartmentOption[];
  employees: SalaryIncrementEmployeeOption[];
}

/** A percentage as the report shows it: two places and a sign for a cut, or a dash when there is none. */
export function percentText(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toFixed(2)}%`;
}

/** The employees the filter offers: everyone, or the chosen department's only. */
export function employeesIn(
  employees: readonly SalaryIncrementEmployeeOption[],
  departmentId: number | null | undefined,
): SalaryIncrementEmployeeOption[] {
  return departmentId ? employees.filter((e) => e.departmentId === departmentId) : [...employees];
}
