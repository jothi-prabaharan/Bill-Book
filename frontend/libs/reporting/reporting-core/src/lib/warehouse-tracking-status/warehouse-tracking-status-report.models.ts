import {
  BatchTrackingWarehouseChoice,
} from '../batch-tracking-status/batch-tracking-status-report.models';

export type {
  BatchTrackingWarehouseChoice,
};

export interface WarehouseTrackingCategoryChoice {
  itemCategoryId: number;
  categoryName: string;
}

export interface WarehouseTrackingStatusReportRequest {
  asOfDate: string;
  warehouseId?: number | null;
  itemCategoryId?: number | null;
  showZeroBalance?: boolean;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface WarehouseTrackingItemDto {
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName?: string | null;
  unitCode?: string | null;
  quantityIn: number;
  quantityOut: number;
  availableQuantity: number;
  averageCost: number;
  estimatedValue: number;
}

export interface WarehouseTrackingGroupDto {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  country?: string | null;
  isPrimary: boolean;
  isActive: boolean;
  totalItems: number;
  totalQuantityIn: number;
  totalQuantityOut: number;
  totalAvailableQuantity: number;
  totalEstimatedValue: number;
  items: WarehouseTrackingItemDto[];
}

export interface WarehouseTrackingTotalsDto {
  totalWarehouses: number;
  totalItems: number;
  totalQuantityIn: number;
  totalQuantityOut: number;
  totalAvailableQuantity: number;
  totalEstimatedValue: number;
}

export interface WarehouseTrackingStatusReportResultDto {
  asOfDate: string;
  currency: string;
  warehouses: WarehouseTrackingGroupDto[];
  totals: WarehouseTrackingTotalsDto;
  totalWarehousesCount: number;
  pageNumber: number;
  pageSize: number;
}
