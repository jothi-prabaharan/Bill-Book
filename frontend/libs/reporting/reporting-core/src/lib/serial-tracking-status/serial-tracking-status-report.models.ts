export interface SerialTrackingStatusReportRequest {
  asOfDate: string;
  itemId?: number | null;
  warehouseId?: number | null;
  status?: string | null;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface SerialTrackingStatusItemDto {
  itemSerialId: number;
  serialNumber: string;
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName?: string | null;
  unitCode?: string | null;
  costingMethod: string;
  hallmarkNumber?: string | null;
  batchNumber?: string | null;
  expiryDate?: string | null;
  status: string;
  warehouseName: string;
  receivedDate?: string | null;
  receivedDocumentNumber?: string | null;
  receivedFromContactName?: string | null;
  soldDate?: string | null;
  soldDocumentNumber?: string | null;
  soldToContactName?: string | null;
  isInStock: boolean;
}

export interface SerialTrackingStatusTotalsDto {
  totalSerials: number;
  inStockCount: number;
  soldCount: number;
  returnedCount: number;
  scrappedCount: number;
}

export interface SerialTrackingStatusReportResultDto {
  asOfDate: string;
  items: SerialTrackingStatusItemDto[];
  totals: SerialTrackingStatusTotalsDto;
  totalItems: number;
  pageNumber: number;
  pageSize: number;
}

export interface SerialTrackingItemChoice {
  itemId: number;
  itemCode: string;
  itemName: string;
}

export interface SerialTrackingWarehouseChoice {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
}
