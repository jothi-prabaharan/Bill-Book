import { InventoryCostingMethod } from '../inventory-item-summary/inventory-item-summary.models';

/**
 * The Inventory Item Detail's contract (TK-244): stock card for each stocked item over a period.
 * For each stocked item: opening balance, all stock movements in the period with document number,
 * source, contact, quantity in/out, unit cost, value, unit sale price, margin, running quantity
 * and value, and closing balance.
 */

export interface InventoryItemDetailRequest {
  fromDate: string;
  toDate: string;
  itemId?: number | null;
  itemCategoryId?: number | null;
  warehouseId?: number | null;
  source?: string | null;
  pageNumber?: number;
  pageSize?: number;
}

export interface InventoryItemDetailMovement {
  stockMovementId: number;
  date: string;
  documentNo: string | null;
  source: string;
  sourceType: string | null;
  sourceId: number | null;
  contactCode: string | null;
  contactName: string | null;
  warehouseName: string | null;
  direction: 'In' | 'Out';
  quantityIn: number | null;
  quantityOut: number | null;
  quantitySigned: number;
  unitCost: number | null;
  value: number | null;
  unitSalePrice: number | null;
  margin: number | null;
  marginPercent: number | null;
  runningQuantity: number;
  runningValue: number;
  costPending: boolean;
  notes: string | null;
}

export interface InventoryItemDetailGroup {
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName: string | null;
  costingType: InventoryCostingMethod;
  unitCode: string | null;
  openingQuantity: number;
  openingValue: number;
  inQuantity: number;
  inValue: number;
  outQuantity: number;
  outValue: number;
  closingQuantity: number;
  closingValue: number;
  costPending: boolean;
  movements: InventoryItemDetailMovement[];
}

export interface InventoryItemDetailTotals {
  openingValue: number;
  inValue: number;
  outValue: number;
  closingValue: number;
  totalMovements: number;
  costPending: boolean;
}

export interface InventoryItemDetailResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  items: InventoryItemDetailGroup[];
  totalItems: number;
  totalMovements: number;
  pageNumber: number;
  pageSize: number;
  totals: InventoryItemDetailTotals;
}

export interface InventoryItemDetailGridRow {
  key: string;
  kind: 'opening' | 'movement' | 'closing';
  date: string;
  documentNo: string | null;
  source: string;
  contactName: string | null;
  warehouseName: string | null;
  quantityIn: number | null;
  quantityOut: number | null;
  quantitySigned: number | null;
  unitCost: number | null;
  value: number | null;
  unitSalePrice: number | null;
  margin: number | null;
  marginPercent: number | null;
  runningQuantity: number;
  runningValue: number;
  costPending: boolean;
}

export function itemDetailRows(item: InventoryItemDetailGroup, fromDate: string, toDate: string): InventoryItemDetailGridRow[] {
  const rows: InventoryItemDetailGridRow[] = [];

  // Opening balance row
  rows.push({
    key: `${item.itemId}-opening`,
    kind: 'opening',
    date: fromDate,
    documentNo: null,
    source: 'Opening Balance',
    contactName: null,
    warehouseName: null,
    quantityIn: null,
    quantityOut: null,
    quantitySigned: null,
    unitCost: null,
    value: item.openingValue,
    unitSalePrice: null,
    margin: null,
    marginPercent: null,
    runningQuantity: item.openingQuantity,
    runningValue: item.openingValue,
    costPending: false,
  });

  // Movement rows
  for (const m of item.movements) {
    rows.push({
      key: `${item.itemId}-${m.stockMovementId}`,
      kind: 'movement',
      date: m.date,
      documentNo: m.documentNo,
      source: m.source,
      contactName: m.contactName ?? m.contactCode,
      warehouseName: m.warehouseName,
      quantityIn: m.quantityIn,
      quantityOut: m.quantityOut,
      quantitySigned: m.quantitySigned,
      unitCost: m.unitCost,
      value: m.value,
      unitSalePrice: m.unitSalePrice,
      margin: m.margin,
      marginPercent: m.marginPercent,
      runningQuantity: m.runningQuantity,
      runningValue: m.runningValue,
      costPending: m.costPending,
    });
  }

  // Closing balance row
  rows.push({
    key: `${item.itemId}-closing`,
    kind: 'closing',
    date: toDate,
    documentNo: null,
    source: 'Closing Balance',
    contactName: null,
    warehouseName: null,
    quantityIn: item.inQuantity,
    quantityOut: item.outQuantity,
    quantitySigned: null,
    unitCost: null,
    value: item.inValue - item.outValue,
    unitSalePrice: null,
    margin: null,
    marginPercent: null,
    runningQuantity: item.closingQuantity,
    runningValue: item.closingValue,
    costPending: item.costPending,
  });

  return rows;
}

export function hasMoreInventoryItemDetail(result: InventoryItemDetailResult): boolean {
  return result.items.length < result.totalItems;
}

export function appendInventoryItemDetailPage(
  current: InventoryItemDetailResult,
  next: InventoryItemDetailResult,
): InventoryItemDetailResult {
  const existingIds = new Set(current.items.map((i) => i.itemId));
  const newItems = next.items.filter((i) => !existingIds.has(i.itemId));
  return {
    ...next,
    items: [...current.items, ...newItems],
  };
}

export function inventoryItemDetailSentence(
  result: InventoryItemDetailResult,
  formatMoney: (value: number) => string,
): string {
  const count = result.totalItems;
  const moves = result.totalMovements;
  const itemNoun = count === 1 ? '1 item' : `${count} items`;
  const moveNoun = moves === 1 ? '1 stock movement' : `${moves} stock movements`;
  const closingStr = formatMoney(result.totals.closingValue);
  return `Showing ${moveNoun} across ${itemNoun}, closing at a total value of ${closingStr} ${result.baseCurrencyCode}.`;
}
