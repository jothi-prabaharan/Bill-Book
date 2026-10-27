export interface FixedAssetsScheduleReportRequest {
  from: string; // ISO date YYYY-MM-DD
  to: string; // ISO date YYYY-MM-DD
  categoryId?: number | null;
  status?: string | null; // 'All' | 'Active' | 'Disposed'
  book?: string | null; // 'Books' | 'Tax'
  search?: string | null;
  orgId?: string;
}

export interface FixedAssetScheduleItemDto {
  fixedAssetId: number;
  categoryId: number;
  categoryName: string;
  assetCode: string;
  assetName: string;
  description?: string | null;
  serialNumber?: string | null;
  assetAccount: string;
  accumDepAccount: string;
  depExpenseAccount: string;
  purchaseDate: string;
  purchasePrice: number;
  depMethod?: string | null;
  depStartDate?: string | null;
  rate?: number | null;
  effectiveLife?: number | null;
  residualValue?: number | null;

  // Cost roll-forward
  openingCost: number;
  additionCost: number;
  disposalCost: number;
  closingCost: number;

  // Accumulated Depreciation roll-forward
  openingAccumDep: number;
  depreciation: number;
  disposalAccumDep: number;
  closingAccumDep: number;

  // Net Book Value
  openingNbv: number;
  closingNbv: number;

  // Disposal details (shown via column chooser)
  disposalDate?: string | null;
  saleProceeds?: number | null;
  nbvAtDisposal?: number | null;
  gainOnDisposal?: number | null;
  lossOnDisposal?: number | null;
  capitalGain?: number | null;

  status: string; // 'Active' | 'Disposed'
}

export interface FixedAssetScheduleCategoryGroupDto {
  categoryId: number;
  categoryName: string;
  assetAccount: string;
  accumDepAccount: string;

  openingCost: number;
  additionCost: number;
  disposalCost: number;
  closingCost: number;

  openingAccumDep: number;
  depreciation: number;
  disposalAccumDep: number;
  closingAccumDep: number;

  openingNbv: number;
  closingNbv: number;

  assets: FixedAssetScheduleItemDto[];
}

export interface FixedAssetsScheduleSummaryDto {
  totalAssets: number;
  activeAssets: number;
  disposedAssets: number;

  totalOpeningCost: number;
  totalAdditionCost: number;
  totalDisposalCost: number;
  totalClosingCost: number;

  totalOpeningAccumDep: number;
  totalDepreciation: number;
  totalDisposalAccumDep: number;
  totalClosingAccumDep: number;

  totalOpeningNbv: number;
  totalClosingNbv: number;
}

export interface FixedAssetsScheduleReportResponse {
  fromDate: string;
  toDate: string;
  book: string;
  summary: FixedAssetsScheduleSummaryDto;
  categories: FixedAssetScheduleCategoryGroupDto[];
}

export interface FixedAssetCategoryOptionDto {
  id: number;
  name: string;
}

export interface FixedAssetsScheduleLookupsDto {
  categories: FixedAssetCategoryOptionDto[];
  statuses: string[];
  books: string[];
}
