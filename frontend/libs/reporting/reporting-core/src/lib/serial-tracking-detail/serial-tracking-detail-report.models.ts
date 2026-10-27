import {
  SerialTrackingItemChoice,
  SerialTrackingWarehouseChoice,
} from '../serial-tracking-status/serial-tracking-status-report.models';

export type {
  SerialTrackingItemChoice,
  SerialTrackingWarehouseChoice,
};

export interface SerialTrackingChoice {
  itemSerialId: number;
  serialNumber: string;
  itemCode: string;
  itemName: string;
}

export interface SerialTrackingContactChoice {
  contactId: number;
  contactCode: string;
  displayName: string;
}

export interface SerialTrackingDetailReportRequest {
  fromDate: string;
  toDate: string;
  itemSerialId?: number | null;
  itemId?: number | null;
  warehouseId?: number | null;
  contactId?: number | null;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface SerialTrackingDetailMovementDto {
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
  notes?: string | null;
}

export interface SerialTrackingDetailGroupDto {
  itemSerialId: number;
  serialNumber: string;
  hallmarkNumber?: string | null;
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName?: string | null;
  unitCode?: string | null;
  costingMethod: string;
  batchNumber?: string | null;
  expiryDate?: string | null;
  warehouseName: string;
  status: string;
  openingQuantity: number;
  totalIn: number;
  totalOut: number;
  closingQuantity: number;
  movements: SerialTrackingDetailMovementDto[];
}

export interface SerialTrackingDetailTotalsDto {
  totalSerials: number;
  totalOpeningQuantity: number;
  totalInQuantity: number;
  totalOutQuantity: number;
  totalClosingQuantity: number;
  totalMovements: number;
}

export interface SerialTrackingDetailReportResultDto {
  fromDate: string;
  toDate: string;
  currency: string;
  serials: SerialTrackingDetailGroupDto[];
  totals: SerialTrackingDetailTotalsDto;
  totalItems: number;
  pageNumber: number;
  pageSize: number;
}
