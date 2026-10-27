export type {
  BatchTrackingItemChoice,
  BatchTrackingWarehouseChoice,
} from '../batch-tracking-status/batch-tracking-status-report.models';

export interface BatchTrackingDetailReportRequest {
  fromDate: string;
  toDate: string;
  batchId?: number | null;
  itemId?: number | null;
  warehouseId?: number | null;
  contactId?: number | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface BatchTrackingDetailMovementDto {
  stockMovementId: number;
  movementDate: string;
  movementType: string;
  sourceType?: string | null;
  sourceId?: number | null;
  documentNumber?: string | null;
  contactName?: string | null;
  warehouseName: string;
  direction: string;
  quantityIn: number;
  quantityOut: number;
  runningBalance: number;
}

export interface BatchTrackingDetailGroupDto {
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
  warehouseName: string;
  openingQuantity: number;
  totalIn: number;
  totalOut: number;
  closingQuantity: number;
  movements: BatchTrackingDetailMovementDto[];
}

export interface BatchTrackingDetailTotalsDto {
  totalBatches: number;
  totalOpeningQuantity: number;
  totalInQuantity: number;
  totalOutQuantity: number;
  totalClosingQuantity: number;
  totalMovements: number;
}

export interface BatchTrackingDetailReportResultDto {
  fromDate: string;
  toDate: string;
  currency: string;
  batches: BatchTrackingDetailGroupDto[];
  totals: BatchTrackingDetailTotalsDto;
  totalItems: number;
  pageNumber: number;
  pageSize: number;
}

export interface BatchTrackingBatchChoice {
  itemBatchId: number;
  batchNumber: string;
  itemCode: string;
  itemName: string;
}

export interface BatchTrackingContactChoice {
  contactId: number;
  contactCode: string;
  displayName: string;
}
