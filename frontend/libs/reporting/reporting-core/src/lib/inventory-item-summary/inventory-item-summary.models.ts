/**
 * The Inventory Item Summary's contract (TK-243), field for field with
 * `InventoryItemSummaryReportModels.cs`, and the pure steps the page takes from an answer: for each stocked
 * item over a period, its opening quantity and value, what was purchased, sold and adjusted, its closing
 * quantity and value, and the profit on what was sold. Quantities are in the item's own inventory unit;
 * values are in the branch's base currency. Stock is one pool per branch, so nothing is split by warehouse.
 * Opening plus purchased less sold plus adjusted is the closing quantity and value of every item. Dates are
 * ISO `yyyy-MM-dd`.
 */

/** An item's costing method, as Inventory names it. */
export type InventoryCostingMethod = 'None' | 'WeightedAverage' | 'Fifo' | 'Lifo' | 'Fefo' | 'SpecificIdentification' | 'MovingAverage';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface InventoryItemSummaryRequest {
  fromDate: string;
  toDate: string;
  /** Only items in this category. */
  itemCategoryId?: number | null;
  /** Only this item. */
  itemId?: number | null;
  /** List every stocked item, including those with no stock at the From date and no movement in the period. */
  showItemsWithNoMovement?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One item's stock over the period. */
export interface InventoryItemSummaryItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryName: string | null;
  costingType: InventoryCostingMethod;
  /** The unit every quantity of the item is in. */
  unitCode: string | null;
  /** The stock at the start of the From date: every movement before it. */
  openingQuantity: number;
  openingValue: number;
  /** Goods received less goods sent back to the vendors, in the period. */
  purchasedQuantity: number;
  purchasedValue: number;
  /** Goods issued to customers less goods they sent back, in the period. */
  soldQuantity: number;
  /** What the quantity sold was sold for: net sales before tax, in base currency. */
  salesValue: number;
  /** What the quantity sold cost. */
  cogs: number;
  /** Sales less cost of sales; null while a movement of what was sold has not been costed yet. */
  profit: number | null;
  /** Every other movement in the period, signed: counts, opening entries, transfers. */
  adjustedQuantity: number;
  adjustedValue: number;
  closingQuantity: number;
  closingValue: number;
  /** A movement up to the To date has not been costed yet, so the values are short by its cost. */
  costPending: boolean;
}

/** The items the filters match added up: values only, since quantities in different units do not add. */
export interface InventoryItemSummaryTotals {
  openingValue: number;
  purchasedValue: number;
  salesValue: number;
  cogs: number;
  /** Null while a movement of something sold has not been costed yet. */
  profit: number | null;
  adjustedValue: number;
  closingValue: number;
  costPending: boolean;
}

export interface InventoryItemSummaryResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of items, by name. */
  items: InventoryItemSummaryItem[];
  /** How many items the filters match, all pages. */
  totalItems: number;
  pageNumber: number;
  pageSize: number;
  /** Added up over every item the filters match, not only this page. */
  totals: InventoryItemSummaryTotals;
}

const COSTING_LABELS: Readonly<Record<InventoryCostingMethod, string>> = {
  None: 'Not costed',
  WeightedAverage: 'Weighted average',
  Fifo: 'FIFO',
  Lifo: 'LIFO',
  Fefo: 'FEFO',
  SpecificIdentification: 'Specific identification',
  MovingAverage: 'Moving average',
};

/** How an item's costing method reads on the page. */
export function costingMethodLabel(method: InventoryCostingMethod): string {
  return COSTING_LABELS[method] ?? method;
}

export type InventoryItemSummaryRowKind = 'item' | 'total';

/** One row of the grid: an item, or the grand total. */
export interface InventoryItemSummaryGridRow {
  kind: InventoryItemSummaryRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** An item's name, or the label of the total. */
  name: string;
  code: string | null;
  categoryName: string | null;
  costing: string | null;
  unitCode: string | null;
  /** Quantities are given on an item and not on the total, where units differ. */
  openingQuantity: number | null;
  openingValue: number;
  purchasedQuantity: number | null;
  purchasedValue: number;
  soldQuantity: number | null;
  salesValue: number;
  cogs: number;
  profit: number | null;
  adjustedQuantity: number | null;
  adjustedValue: number;
  closingQuantity: number | null;
  closingValue: number;
  costPending: boolean;
}

/**
 * The grid's rows: a row for each item, then the total of every item the filters match, which is the
 * server's and not the loaded items' sum, so it stays right until the last page is loaded. Nothing is
 * worked out here beyond the layout.
 */
export function inventoryItemSummaryRows(result: InventoryItemSummaryResult): InventoryItemSummaryGridRow[] {
  const rows: InventoryItemSummaryGridRow[] = result.items.map((item) => ({
    kind: 'item',
    key: `i:${item.itemId}`,
    name: item.itemName,
    code: item.itemCode,
    categoryName: item.categoryName,
    costing: costingMethodLabel(item.costingType),
    unitCode: item.unitCode,
    openingQuantity: item.openingQuantity,
    openingValue: item.openingValue,
    purchasedQuantity: item.purchasedQuantity,
    purchasedValue: item.purchasedValue,
    soldQuantity: item.soldQuantity,
    salesValue: item.salesValue,
    cogs: item.cogs,
    profit: item.profit,
    adjustedQuantity: item.adjustedQuantity,
    adjustedValue: item.adjustedValue,
    closingQuantity: item.closingQuantity,
    closingValue: item.closingValue,
    costPending: item.costPending,
  }));

  if (result.items.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: result.items.length < result.totalItems ? `Total of all ${result.totalItems} items` : 'Total',
      code: null,
      categoryName: null,
      costing: null,
      unitCode: null,
      openingQuantity: null,
      openingValue: t.openingValue,
      purchasedQuantity: null,
      purchasedValue: t.purchasedValue,
      soldQuantity: null,
      salesValue: t.salesValue,
      cogs: t.cogs,
      profit: t.profit,
      adjustedQuantity: null,
      adjustedValue: t.adjustedValue,
      closingQuantity: null,
      closingValue: t.closingValue,
      costPending: t.costPending,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals are the latest answer's. */
export function appendInventoryItemSummaryPage(loaded: InventoryItemSummaryResult, next: InventoryItemSummaryResult): InventoryItemSummaryResult {
  return { ...next, items: [...loaded.items, ...next.items] };
}

/** Whether there are items the filters match that are not loaded yet. */
export function hasMoreInventoryItemSummary(result: Pick<InventoryItemSummaryResult, 'totalItems' | 'items'>): boolean {
  return result.items.length < result.totalItems;
}

/**
 * What the period came to, in a sentence: how many items, and what the stock was worth at the start and the
 * end. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function inventoryItemSummarySentence(
  result: Pick<InventoryItemSummaryResult, 'totalItems' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalItems;
  const what = n === 1 ? '1 item' : `${n} items`;
  const t = result.totals;
  const pending = t.costPending ? ' Some movements have not been costed yet, so these values are short by what they cost.' : '';

  return `${what}: stock worth ${money(t.openingValue)} at the start and ${money(t.closingValue)} at the end.${pending}`;
}
