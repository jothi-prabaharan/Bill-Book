export interface InventoryAgingReportRequest {
  asOfDate: string;
  itemCategoryId?: number | null;
  warehouseId?: number | null;
  onlyWithStock?: boolean;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface InventoryAgingBucketDto {
  quantity: number;
  value: number;
}

export interface InventoryAgingItemDto {
  itemId: number;
  itemCode: string;
  itemName: string;
  itemCategoryId?: number | null;
  categoryName?: string | null;
  unitCode?: string | null;
  costingType: string;
  totalQuantity: number;
  totalValue: number;
  bucket0To30: InventoryAgingBucketDto;
  bucket31To60: InventoryAgingBucketDto;
  bucket61To90: InventoryAgingBucketDto;
  bucket91To180: InventoryAgingBucketDto;
  bucketOver180: InventoryAgingBucketDto;
}

export interface InventoryAgingTotalsDto {
  totalQuantity: number;
  totalValue: number;
  bucket0To30: InventoryAgingBucketDto;
  bucket31To60: InventoryAgingBucketDto;
  bucket61To90: InventoryAgingBucketDto;
  bucket91To180: InventoryAgingBucketDto;
  bucketOver180: InventoryAgingBucketDto;
}

export interface InventoryAgingReportDto {
  asOfDate: string;
  baseCurrencyCode: string;
  items: InventoryAgingItemDto[];
  totalItems: number;
  pageNumber: number;
  pageSize: number;
  totals: InventoryAgingTotalsDto;
}

export interface InventoryAgingCategoryChoice {
  itemCategoryId: number;
  categoryName: string;
}

export interface InventoryAgingWarehouseChoice {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
}
