/**
 * The Sales Analysis Detail Report's contract (TK-229), field for field with
 * `SalesAnalysisDetailReportModels.cs`, and the pure steps the page takes from an answer: the Sales
 * Analysis before it is added up, one row for each invoice or credit note line, in groups (item, item
 * category or customer) each with its subtotal. Everything is in the branch's base currency, a credit
 * note's lines are negative, and a line whose cost is not settled yet is marked pending and has no
 * margin. A group can run over onto the next page, whose answer repeats its heading. Dates are ISO
 * `yyyy-MM-dd`.
 */

import { SalesAnalysisGroup, SalesAnalysisGroupBy, SalesAnalysisTotals } from '../sales-analysis/sales-analysis.models';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface SalesAnalysisDetailRequest {
  fromDate: string;
  toDate: string;
  groupBy?: SalesAnalysisGroupBy;
  /** Only lines for this item; every item when blank. */
  itemId?: number | null;
  /** Only lines for items in this category; every category when blank. */
  itemCategoryId?: number | null;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only lines sold out of this warehouse; every warehouse when blank. */
  warehouseId?: number | null;
  /** Whether posted credit notes' lines are listed, as negatives; on unless turned off. */
  includeCreditNotes?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One invoice or credit note line. */
export interface SalesAnalysisDetailLine {
  /** Unique across the report: an invoice line's id, or a credit note line's id negated. */
  lineKey: number;
  /** INV or POS for an invoice, CRN for a credit note. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  lineNumber: number;
  contactId: number;
  contactName: string;
  /** Nought for a line with no item. */
  itemId: number;
  itemCode: string;
  itemName: string;
  quantity: number;
  /** The price of one unit, in base currency. */
  unitPrice: number;
  gross: number;
  discount: number;
  /** Net sales: before tax, after discount. */
  net: number;
  cost: number;
  /** Whether the line's cost has not been settled yet. */
  costPending: boolean;
  /** Net sales less cost; null while the cost is pending. */
  margin: number | null;
}

/** A group with its lines on this page and its subtotal over every line it has. */
export interface SalesAnalysisDetailGroup {
  key: number;
  code: string;
  name: string;
  lines: SalesAnalysisDetailLine[];
  /** How many lines the group has in all, not only on this page. */
  lineCount: number;
  subtotal: SalesAnalysisGroup;
}

export interface SalesAnalysisDetailResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  groupBy: SalesAnalysisGroupBy;
  includesCreditNotes: boolean;
  /** The groups whose lines fall on this page, in order. */
  groups: SalesAnalysisDetailGroup[];
  /** How many lines the filters match, all pages. */
  totalLines: number;
  pageNumber: number;
  pageSize: number;
  /** Everything the filters match added up, not only this page. */
  totals: SalesAnalysisTotals;
}

export type SalesAnalysisDetailRowKind = 'group' | 'line' | 'subtotal' | 'total';

/** One row of the grid: a group's heading, one of its lines, its subtotal, or the grand total. */
export interface SalesAnalysisDetailGridRow {
  kind: SalesAnalysisDetailRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** A group's name, a document's number, or the label of a subtotal or total row. */
  name: string;
  /** A group's code, or an item's. */
  code: string;
  /** Where a document number opens; null off a line. */
  link: string | null;
  date: string | null;
  /** The customer on a line, or the item when grouped by customer: the other half of what the line is. */
  who: string;
  quantity: number | null;
  unitPrice: number | null;
  discount: number | null;
  net: number | null;
  cost: number | null;
  costPending: boolean;
  /** How many lines a subtotal or group heading stands for. */
  lineCount: number;
  margin: number | null;
}

/** Where a line's document opens: an invoice or POS sale, or a credit note. */
export function detailDocumentLink(line: Pick<SalesAnalysisDetailLine, 'typeCode' | 'documentId'>): string | null {
  switch (line.typeCode) {
    case 'INV':
    case 'POS':
      return `/sales/invoices/${line.documentId}`;
    case 'CRN':
      return `/sales/credit-notes/${line.documentId}`;
    default:
      return null;
  }
}

const blank = (kind: SalesAnalysisDetailRowKind, key: string, name: string): SalesAnalysisDetailGridRow => ({
  kind,
  key,
  name,
  code: '',
  link: null,
  date: null,
  who: '',
  quantity: null,
  unitPrice: null,
  discount: null,
  net: null,
  cost: null,
  costPending: false,
  lineCount: 0,
  margin: null,
});

/**
 * The grid's rows: for each group a heading, its lines, and its subtotal; then the total of everything
 * the filters match, which is the server's and not the loaded lines' sum. A subtotal is of every line
 * the group has, so it is right even while only some of them are loaded. Nothing is worked out here
 * beyond the layout: a line shows the customer, or the item when the lines are grouped by customer.
 */
export function salesAnalysisDetailRows(result: SalesAnalysisDetailResult): SalesAnalysisDetailGridRow[] {
  const rows: SalesAnalysisDetailGridRow[] = [];
  const byCustomer = result.groupBy === 'Customer';

  for (const group of result.groups) {
    rows.push({ ...blank('group', `g:${group.key}`, group.name), code: group.code, lineCount: group.lineCount });

    for (const line of group.lines) {
      rows.push({
        ...blank('line', `l:${line.lineKey}`, line.documentNo),
        code: line.itemCode,
        link: detailDocumentLink(line),
        date: line.documentDate,
        who: byCustomer ? line.itemName : line.contactName,
        quantity: line.quantity,
        unitPrice: line.unitPrice,
        discount: line.discount,
        net: line.net,
        cost: line.cost,
        costPending: line.costPending,
        margin: line.margin,
      });
    }

    const s = group.subtotal;
    rows.push({
      ...blank('subtotal', `s:${group.key}`, group.lineCount === 1 ? 'Subtotal, 1 line' : `Subtotal, ${group.lineCount} lines`),
      quantity: s.quantity,
      discount: s.discount,
      net: s.net,
      cost: s.cost,
      costPending: s.costPendingLines > 0,
      lineCount: group.lineCount,
      margin: s.margin,
    });
  }

  if (result.groups.length > 0) {
    const t = result.totals;
    rows.push({
      ...blank('total', 't', 'Total'),
      quantity: t.quantity,
      discount: t.discount,
      net: t.net,
      cost: t.cost,
      costPending: t.costPendingLines > 0,
      lineCount: result.totalLines,
      margin: t.margin,
    });
  }

  return rows;
}

/**
 * The pages loaded so far with the next one added. A group that runs over the page break is in both
 * answers, so its lines are joined into one group, with the latest subtotal. The totals are the
 * latest answer's.
 */
export function appendSalesAnalysisDetailPage(loaded: SalesAnalysisDetailResult, next: SalesAnalysisDetailResult): SalesAnalysisDetailResult {
  const groups = [...loaded.groups];
  const last = groups[groups.length - 1];

  next.groups.forEach((group, index) => {
    if (index === 0 && last && last.key === group.key) {
      groups[groups.length - 1] = { ...group, lines: [...last.lines, ...group.lines] };
    } else {
      groups.push(group);
    }
  });

  return { ...next, groups };
}

/** How many lines are loaded, across the groups. */
export function loadedDetailLines(result: Pick<SalesAnalysisDetailResult, 'groups'>): number {
  return result.groups.reduce((sum, g) => sum + g.lines.length, 0);
}

/** Whether there are lines the filters match that are not loaded yet. */
export function hasMoreSalesAnalysisDetail(result: Pick<SalesAnalysisDetailResult, 'groups' | 'totalLines'>): boolean {
  return loadedDetailLines(result) < result.totalLines;
}

/**
 * What the period came to, in a sentence: how many lines and what they sold for, and the margin when
 * every cost is settled or how many lines are still waiting for theirs. Money is formatted by the
 * caller so it reads in the branch's own currency.
 */
export function salesAnalysisDetailSentence(
  result: Pick<SalesAnalysisDetailResult, 'totals' | 'totalLines'>,
  money: (value: number) => string,
): string {
  const t = result.totals;
  const what = result.totalLines === 1 ? '1 line' : `${result.totalLines} lines`;

  if (t.costPendingLines > 0) {
    return `${what}: net sales ${money(t.net)}. ${t.costPendingLines === 1 ? '1 line is' : `${t.costPendingLines} lines are`} not costed yet, so the margin is not known yet.`;
  }

  return `${what}: net sales ${money(t.net)}, cost ${money(t.cost)}, margin ${money(t.margin ?? 0)}.`;
}
