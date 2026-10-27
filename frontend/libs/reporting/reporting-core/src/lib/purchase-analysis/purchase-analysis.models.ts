/**
 * The Purchase Analysis's contract (TK-239), field for field with `PurchaseAnalysisReportModels.cs`,
 * and the pure steps the page takes from an answer: what was bought in a period, added up by item,
 * item category or vendor, with quantity, gross value, discount, net purchases (before tax), the
 * average cost of a unit (of an item), each group's share of the whole, and optionally net purchases in a second
 * period. Everything is in the branch's base currency. Dates are ISO `yyyy-MM-dd`.
 */

/** What purchases are added up by. */
export type PurchaseAnalysisGroupBy = 'Item' | 'Category' | 'Vendor';

/** What the report is asked for. Both dates are required; the rest narrow or extend it. */
export interface PurchaseAnalysisRequest {
  fromDate: string;
  toDate: string;
  groupBy?: PurchaseAnalysisGroupBy;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only items in this category; every category when blank. */
  itemCategoryId?: number | null;
  /** Whether posted debit notes reduce the figures; on unless turned off. */
  includeDebitNotes?: boolean;
  /** Whether lines bought as fixed assets are counted; off unless asked for. */
  includeFixedAssets?: boolean;
  /** A second period to compare net purchases with; both dates or neither. */
  compareFromDate?: string | null;
  compareToDate?: string | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One item, category or vendor with what was bought from it. */
export interface PurchaseAnalysisGroup {
  /** The item, category or vendor id; nought for the row of lines with none. */
  key: number;
  code: string;
  name: string;
  /** Units bought less units returned, in each item's base unit. */
  quantity: number;
  /** Quantity times price, as keyed. */
  gross: number;
  discount: number;
  /** What was bought for before tax, after discount. */
  net: number;
  /** Net purchases per unit; given only to an item when adding up by item, and null with no units. */
  averageCost: number | null;
  /** This group's share of all net purchases, as a percentage; null when the total is nought. */
  sharePercent: number | null;
  /** Net purchases in the comparison period; null when none was asked for. */
  compareNet: number | null;
  /** How net purchases moved against the comparison period, as a percentage of it. */
  changePercent: number | null;
}

/** Everything the filters match added up, not only the groups on this page. */
export interface PurchaseAnalysisTotals {
  quantity: number;
  gross: number;
  discount: number;
  net: number;
  compareNet: number | null;
  changePercent: number | null;
}

export interface PurchaseAnalysisResult {
  fromDate: string;
  toDate: string;
  compareFromDate: string | null;
  compareToDate: string | null;
  baseCurrencyCode: string;
  groupBy: PurchaseAnalysisGroupBy;
  includesDebitNotes: boolean;
  includesFixedAssets: boolean;
  /** This page of groups, largest net purchases first. */
  groups: PurchaseAnalysisGroup[];
  /** How many groups the filters match, all pages. */
  totalGroups: number;
  pageNumber: number;
  pageSize: number;
  totals: PurchaseAnalysisTotals;
}

/** What to group by, in the order the filter offers it. */
export const PURCHASE_ANALYSIS_GROUPINGS: readonly { value: PurchaseAnalysisGroupBy; label: string; heading: string }[] = [
  { value: 'Item', label: 'Item', heading: 'Item' },
  { value: 'Category', label: 'Item category', heading: 'Item category' },
  { value: 'Vendor', label: 'Vendor', heading: 'Vendor' },
];

/** The column heading for what the rows are. */
export function purchaseGroupingHeading(groupBy: PurchaseAnalysisGroupBy): string {
  return PURCHASE_ANALYSIS_GROUPINGS.find((g) => g.value === groupBy)?.heading ?? 'Group';
}

export type PurchaseAnalysisRowKind = 'group' | 'total';

/** One row of the grid: a group, or the totals of everything the filters match. */
export interface PurchaseAnalysisGridRow {
  kind: PurchaseAnalysisRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  name: string;
  /** The item's, category's or vendor's code; blank on the totals row and where there is none. */
  code: string;
  quantity: number;
  gross: number;
  discount: number;
  net: number;
  averageCost: number | null;
  sharePercent: number | null;
  compareNet: number | null;
  changePercent: number | null;
}

/**
 * The grid's rows: each group as the server sent it, then the totals of everything the filters match,
 * which are the server's and not the loaded rows' sum, so they stay right until the last page is
 * loaded. The totals row's share is the whole, a hundred. Nothing is worked out here beyond the layout.
 */
export function purchaseAnalysisRows(result: PurchaseAnalysisResult): PurchaseAnalysisGridRow[] {
  const rows: PurchaseAnalysisGridRow[] = result.groups.map((g) => ({
    kind: 'group',
    key: `g:${g.key}`,
    name: g.name,
    code: g.code,
    quantity: g.quantity,
    gross: g.gross,
    discount: g.discount,
    net: g.net,
    averageCost: g.averageCost,
    sharePercent: g.sharePercent,
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
      averageCost: null,
      sharePercent: t.net === 0 ? null : 100,
      compareNet: t.compareNet,
      changePercent: t.changePercent,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the totals are the latest answer's. */
export function appendPurchaseAnalysisPage(loaded: PurchaseAnalysisResult, next: PurchaseAnalysisResult): PurchaseAnalysisResult {
  return { ...next, groups: [...loaded.groups, ...next.groups] };
}

/** Whether there are groups the filters match that are not loaded yet. */
export function hasMorePurchaseAnalysis(result: Pick<PurchaseAnalysisResult, 'totalGroups' | 'groups'>): boolean {
  return result.groups.length < result.totalGroups;
}

/**
 * What the period came to, in a sentence: how many groups and what was bought in all. Money is
 * formatted by the caller so it reads in the branch's own currency.
 */
export function purchaseAnalysisSentence(
  result: Pick<PurchaseAnalysisResult, 'totals' | 'totalGroups' | 'groupBy'>,
  money: (value: number) => string,
): string {
  const t = result.totals;
  const what = `${result.totalGroups} ${purchaseGroupNoun(result.groupBy, result.totalGroups)}`;

  return `${what}: net purchases ${money(t.net)} for ${t.quantity} units, ${money(t.gross)} gross less ${money(t.discount)} discount.`;
}

function purchaseGroupNoun(groupBy: PurchaseAnalysisGroupBy, count: number): string {
  const one = groupBy === 'Category' ? 'category' : groupBy === 'Vendor' ? 'vendor' : 'item';
  const many = groupBy === 'Category' ? 'categories' : groupBy === 'Vendor' ? 'vendors' : 'items';
  return count === 1 ? one : many;
}
