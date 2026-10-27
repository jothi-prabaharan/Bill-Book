export interface BatchTrackingStatusReportRequest {
  asOfDate: string;
  itemId?: number | null;
  warehouseId?: number | null;
  expiringWithinDays?: number | null;
  expiredOnly?: boolean;
  withStockOnly?: boolean;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface BatchTrackingStatusItemDto {
  itemBatchId: number;
  batchNumber: string;
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName?: string | null;
  unitCode?: string | null;
  manufactureDate?: string | null;
  expiryDate?: string | null;
  costingMethod: string;
  warehouseId?: number | null;
  warehouseName: string;
  receivedQuantity: number;
  availableQuantity: number;
  mrp?: number | null;
  daysToExpiry?: number | null;
  isExpired: boolean;
  isNearExpiry: boolean;
}

export interface BatchTrackingStatusTotalsDto {
  totalReceivedQuantity: number;
  totalAvailableQuantity: number;
  expiredBatchCount: number;
  nearExpiryBatchCount: number;
}

export interface BatchTrackingStatusReportDto {
  asOfDate: string;
  items: BatchTrackingStatusItemDto[];
  totalItems: number;
  pageNumber: number;
  pageSize: number;
  totals: BatchTrackingStatusTotalsDto;
}

export interface BatchTrackingItemChoice {
  itemId: number;
  itemCode: string;
  itemName: string;
}

export interface BatchTrackingWarehouseChoice {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
}
