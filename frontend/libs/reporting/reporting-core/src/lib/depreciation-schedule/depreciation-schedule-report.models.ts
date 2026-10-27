export interface DepreciationScheduleReportRequest {
  from: string; // ISO date YYYY-MM-DD
  to: string; // ISO date YYYY-MM-DD
  categoryId?: number | null;
  method?: string | null; // 'All' | 'Straight Line' | 'Written Down Value'
  book?: string | null; // 'Books' | 'Tax'
  search?: string | null;
  orgId?: string;
}

export interface DepreciationScheduleItemDto {
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
  cost: number;
  depMethod?: string | null;
  depStartDate?: string | null;
  rate?: number | null;
  effectiveLife?: number | null;
  residualValue?: number | null;

  // Depreciation roll-forward
  openingAccumDep: number;
  depreciation: number;
  disposalAccumDep: number;
  closingAccumDep: number;

  // Book Value
  openingNbv: number;
  closingNbv: number;

  disposalDate?: string | null;
  status: string; // 'Active' | 'Disposed'
}

export interface DepreciationScheduleCategoryGroupDto {
  categoryId: number;
  categoryName: string;
  assetAccount: string;
  accumDepAccount: string;
  depExpenseAccount: string;

  totalCost: number;
  openingAccumDep: number;
  depreciation: number;
  disposalAccumDep: number;
  closingAccumDep: number;
  openingNbv: number;
  closingNbv: number;

  assets: DepreciationScheduleItemDto[];
}

export interface DepreciationScheduleSummaryDto {
  totalAssets: number;
  activeAssets: number;
  disposedAssets: number;

  totalCost: number;
  totalOpeningAccumDep: number;
  totalDepreciation: number;
  totalDisposalAccumDep: number;
  totalClosingAccumDep: number;
  totalOpeningNbv: number;
  totalClosingNbv: number;
}

export interface DepreciationScheduleReportResponse {
  fromDate: string;
  toDate: string;
  book: string;
  summary: DepreciationScheduleSummaryDto;
  categories: DepreciationScheduleCategoryGroupDto[];
}

export interface DepreciationScheduleLookupsDto {
  categories: { id: number; name: string }[];
  methods: string[];
  books: string[];
}
