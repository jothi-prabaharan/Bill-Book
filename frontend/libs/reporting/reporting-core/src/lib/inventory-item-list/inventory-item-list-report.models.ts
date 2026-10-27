export interface InventoryItemListReportRequest {
  asOfDate: string;
  itemCategoryId?: number | null;
  status?: string | null;
  itemType?: string | null;
  onlyWithStock?: boolean;
  search?: string | null;
  pageNumber?: number;
  pageSize?: number;
  orgId?: string | null;
}

export interface InventoryItemListItemDto {
  itemId: number;
  itemCode: string;
  itemName: string;
  itemCategoryId?: number | null;
  categoryName?: string | null;
  itemType: string;
  costingType: string;
  status: string;
  unitCode?: string | null;
  description?: string | null;
  purchasePrice?: number | null;
  salesPrice?: number | null;
  purchaseTaxRate?: number | null;
  salesTaxRate?: number | null;
  inventoryAccount?: string | null;
  purchaseAccount?: string | null;
  salesAccount?: string | null;
  quantityOnHand: number;
  averageCost: number;
  unitCostPriceFifo: number;
  totalValue: number;
  quantityOnOrder: number;
  quantityCommitted: number;
  balanceQty: number;
  costPending: boolean;
}

export interface InventoryItemListTotalsDto {
  totalQuantityOnHand: number;
  totalValue: number;
  totalQuantityOnOrder: number;
  totalQuantityCommitted: number;
  totalBalanceQty: number;
  costPending: boolean;
}

export interface InventoryItemListReportDto {
  asOfDate: string;
  baseCurrencyCode: string;
  items: InventoryItemListItemDto[];
  totalItems: number;
  pageNumber: number;
  pageSize: number;
  totals: InventoryItemListTotalsDto;
}

export interface InventoryItemListCategoryChoice {
  itemCategoryId: number;
  categoryName: string;
}

export interface InventoryItemListLookupChoice {
  key: string;
  label: string;
}
