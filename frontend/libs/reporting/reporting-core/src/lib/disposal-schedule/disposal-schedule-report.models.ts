export interface DisposalScheduleReportRequest {
  from: string; // ISO date YYYY-MM-DD
  to: string; // ISO date YYYY-MM-DD
  categoryId?: number | null;
  search?: string | null;
  orgId?: string;
}

export interface DisposalScheduleCategoryLookupDto {
  id: number;
  name: string;
  disposedCount: number;
}

export interface DisposalScheduleLookupsDto {
  categories: DisposalScheduleCategoryLookupDto[];
}

export interface DisposalScheduleItemDto {
  fixedAssetId: number;
  categoryId: number;
  categoryName: string;
  assetCode: string;
  assetName: string;
  description?: string | null;
  serialNumber?: string | null;
  assetAccount: string;
  accumDepAccount: string;
  purchaseDate: string;
  cost: number;
  depMethod?: string | null;
  disposalDate: string;
  accumDep: number;
  nbvAtDisposal: number;
  saleProceeds: number;
  gainOnDisposal: number;
  capitalGain: number;
  lossOnDisposal: number;
  reference?: string | null;
}

export interface DisposalScheduleCategoryGroupDto {
  categoryId: number;
  categoryName: string;
  assetCount: number;
  totalCost: number;
  totalAccumDep: number;
  totalNbvAtDisposal: number;
  totalProceeds: number;
  totalGain: number;
  totalCapitalGain: number;
  totalLoss: number;
  netGainLoss: number;
  disposals: DisposalScheduleItemDto[];
}

export interface DisposalScheduleSummaryDto {
  totalDisposals: number;
  totalCost: number;
  totalAccumDep: number;
  totalNbvAtDisposal: number;
  totalProceeds: number;
  totalGain: number;
  totalCapitalGain: number;
  totalLoss: number;
  netGainLoss: number;
}

export interface DisposalScheduleReportResponse {
  fromDate: string;
  toDate: string;
  currency: string;
  categories: DisposalScheduleCategoryGroupDto[];
  summary: DisposalScheduleSummaryDto;
}
