export interface FixedAssetReconciliationReportRequest {
  from: string; // ISO date YYYY-MM-DD
  to: string; // ISO date YYYY-MM-DD
  orgId?: string;
}

export interface ReconciliationSideBalanceDto {
  openingCost: number;
  costDebits: number;
  costCredits: number;
  closingCost: number;

  openingAccumDep: number;
  accumDepDebits: number;
  accumDepCredits: number;
  closingAccumDep: number;

  openingBookValue: number;
  closingBookValue: number;

  closingBalance: number;
}

export interface FixedAssetReconciliationAccountDto {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountType: 'Asset' | 'AccumulatedDepreciation';

  register: ReconciliationSideBalanceDto;
  ledger: ReconciliationSideBalanceDto;

  closingDifference: number;
  isReconciled: boolean;
  differenceExplanation: string;
}

export interface FixedAssetReconciliationSummaryDto {
  totalAccounts: number;
  reconciledAccounts: number;
  discrepantAccounts: number;

  registerTotalNbv: number;
  ledgerTotalNbv: number;
  totalNbvDifference: number;
  explanation: string;
}

export interface FixedAssetReconciliationReportResponse {
  fromDate: string;
  toDate: string;
  currency: string;
  accounts: FixedAssetReconciliationAccountDto[];
  summary: FixedAssetReconciliationSummaryDto;
}
