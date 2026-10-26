/**
 * The Sales Analysis's contract (TK-228), field for field with `SalesAnalysisReportModels.cs`, and
 * the pure steps the page takes from an answer: what sold in a period, added up by item, item
 * category or customer, with quantity, gross value, discount, net sales (before tax), cost of sales
 * and margin, and optionally net sales in a second period. Everything is in the branch's base
 * currency. A line whose cost has not been settled yet is counted, its cost is said to be pending,
 * and the group has no margin until it is. Dates are ISO `yyyy-MM-dd`.
 */

/** What sales are added up by. */
export type SalesAnalysisGroupBy = 'Item' | 'Category' | 'Customer';

/** What the report is asked for. Both dates are required; the rest narrow or extend it. */
export interface SalesAnalysisRequest {
  fromDate: string;
  toDate: string;
  groupBy?: SalesAnalysisGroupBy;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only items in this category; every category when blank. */
  itemCategoryId?: number | null;
  /** Only lines sold out of this warehouse; every warehouse when blank. */
  warehouseId?: number | null;
  /** Whether posted credit notes reduce the figures; on unless turned off. */
  includeCreditNotes?: boolean;
  /** A second period to compare net sales with; both dates or neither. */
  compareFromDate?: string | null;
  compareToDate?: string | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One item, category or customer with what it sold. */
export interface SalesAnalysisGroup {
  /** The item, category or customer id; nought for the row of lines with none. */
  key: number;
  code: string;
  name: string;
  /** Units sold less units returned, in each item's base unit. */
  quantity: number;
  /** Quantity times price, as keyed. */
  gross: number;
  discount: number;
  /** What was sold for before tax, after discount. */
  net: number;
  /** What the costed lines cost; a line not costed yet is not in it. */
  cost: number;
  /** How many lines have no settled cost yet. */
  costPendingLines: number;
  /** Net sales less cost; null while any line's cost is pending. */
  margin: number | null;
  marginPercent: number | null;
  /** Net sales in the comparison period; null when none was asked for. */
  compareNet: number | null;
  /** How net sales moved against the comparison period, as a percentage of it. */
  changePercent: number | null;
}

/** Everything the filters match added up, not only the groups on this page. */
export interface SalesAnalysisTotals {
  quantity: number;
  gross: number;
  discount: number;
  net: number;
  cost: number;
  costPendingLines: number;
  margin: number | null;
  marginPercent: number | null;
  compareNet: number | null;
  changePercent: number | null;
}

export interface SalesAnalysisResult {
  fromDate: string;
  toDate: string;
  compareFromDate: string | null;
  compareToDate: string | null;
  baseCurrencyCode: string;
  groupBy: SalesAnalysisGroupBy;
  includesCreditNotes: boolean;
  /** This page of groups, largest net sales first. */
  groups: SalesAnalysisGroup[];
  /** How many groups the filters match, all pages. */
  totalGroups: number;
  pageNumber: number;
  pageSize: number;
  totals: SalesAnalysisTotals;
}

/** A warehouse the filter can pick. */
export interface SalesAnalysisWarehouseChoice {
  warehouseId: number;
  warehouseCode: string;
  warehouseName: string;
}

/** What to group by, in the order the filter offers it. */
export const SALES_ANALYSIS_GROUPINGS: readonly { value: SalesAnalysisGroupBy; label: string; heading: string }[] = [
  { value: 'Item', label: 'Item', heading: 'Item' },
  { value: 'Category', label: 'Item category', heading: 'Item category' },
  { value: 'Customer', label: 'Customer', heading: 'Customer' },
];

/** The column heading for what the rows are. */
export function groupingHeading(groupBy: SalesAnalysisGroupBy): string {
  return SALES_ANALYSIS_GROUPINGS.find((g) => g.value === groupBy)?.heading ?? 'Group';
}

/** How a warehouse reads in the filter: its name, then its code. */
export function warehouseChoiceLabel(warehouse: SalesAnalysisWarehouseChoice): string {
  return warehouse.warehouseCode ? `${warehouse.warehouseName} (${warehouse.warehouseCode})` : warehouse.warehouseName;
}

export type SalesAnalysisRowKind = 'group' | 'total';

/** One row of the grid: a group, or the totals of everything the filters match. */
export interface SalesAnalysisGridRow {
  kind: SalesAnalysisRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  name: string;
  /** The item's, category's or customer's code; blank on the totals row and where there is none. */
  code: string;
  quantity: number;
  gross: number;
  discount: number;
  net: number;
  cost: number;
  /** How many lines have no settled cost yet. */
  costPendingLines: number;
  margin: number | null;
  marginPercent: number | null;
  compareNet: number | null;
  changePercent: number | null;
}

/**
 * The grid's rows: each group as the server sent it, then the totals of everything the filters match,
 * which are the server's and not the loaded rows' sum, so they stay right until the last page is
 * loaded. Nothing is worked out here beyond the layout.
 */
export function salesAnalysisRows(result: SalesAnalysisResult): SalesAnalysisGridRow[] {
  const rows: SalesAnalysisGridRow[] = result.groups.map((g) => ({
    kind: 'group',
    key: `g:${g.key}`,
    name: g.name,
    code: g.code,
    quantity: g.quantity,
    gross: g.gross,
    discount: g.discount,
    net: g.net,
    cost: g.cost,
    costPendingLines: g.costPendingLines,
    margin: g.margin,
    marginPercent: g.marginPercent,
    compareNet: g.compareNet,
    changePercent: g.changePercent,
  }));

  if (result.groups.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: result.groups.length < result.totalGroups ? `Total of all ${result.totalGroups}` : 'Total',
      code: '',
      quantity: t.quantity,
      gross: t.gross,
      discount: t.discount,
      net: t.net,
      cost: t.cost,
      costPendingLines: t.costPendingLines,
      margin: t.margin,
      marginPercent: t.marginPercent,
      compareNet: t.compareNet,
      changePercent: t.changePercent,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals are the latest answer's. */
export function appendSalesAnalysisPage(loaded: SalesAnalysisResult, next: SalesAnalysisResult): SalesAnalysisResult {
  return { ...next, groups: [...loaded.groups, ...next.groups] };
}

/** Whether there are groups the filters match that are not loaded yet. */
export function hasMoreSalesAnalysis(result: Pick<SalesAnalysisResult, 'totalGroups' | 'groups'>): boolean {
  return result.groups.length < result.totalGroups;
}

/** What a pending cost reads as: how many lines are waiting for their cost. */
export function costPendingText(lines: number): string {
  return lines === 1 ? '1 line not costed yet' : `${lines} lines not costed yet`;
}

/**
 * What the period came to, in a sentence: net sales, and the cost and margin when every cost is
 * settled, or how many lines are still waiting for theirs. Money is formatted by the caller so it
 * reads in the branch's own currency.
 */
export function salesAnalysisSentence(
  result: Pick<SalesAnalysisResult, 'totals' | 'totalGroups' | 'groupBy'>,
  money: (value: number) => string,
): string {
  const t = result.totals;
  const what = `${result.totalGroups} ${groupNoun(result.groupBy, result.totalGroups)}`;

  if (t.costPendingLines > 0) {
    return `${what}: net sales ${money(t.net)}. ${costPendingText(t.costPendingLines)}, so the margin is not known yet.`;
  }

  const percent = t.marginPercent === null ? '' : ` (${t.marginPercent.toFixed(2)}%)`;
  return `${what}: net sales ${money(t.net)}, cost ${money(t.cost)}, margin ${money(t.margin ?? 0)}${percent}.`;
}

function groupNoun(groupBy: SalesAnalysisGroupBy, count: number): string {
  const one = groupBy === 'Category' ? 'category' : groupBy === 'Customer' ? 'customer' : 'item';
  const many = groupBy === 'Category' ? 'categories' : groupBy === 'Customer' ? 'customers' : 'items';
  return count === 1 ? one : many;
}
