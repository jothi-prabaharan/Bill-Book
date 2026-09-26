/**
 * The approvals inbox (TK-103): what waits on the signed-in user, gathered
 * from every RetailErp service that stores approval steps. Each service
 * answers only for its own documents, under its own route prefix (the gateway
 * routes by prefix), so the inbox asks them all and merges the answers.
 */

/** One waiting document, as `ApprovalInboxItem` reports it. */
export interface InboxItem {
  /** purchase, sales, accounting or inventory. */
  service: string;
  /** The route segment the document's API sits under, e.g. purchase-orders. */
  document: string;
  requestKind: string;
  requestId: number;
  documentNo: string;
  documentDate: string;
  amount: number;
  /** The level it waits on, e.g. "Accountant". */
  label: string;
}

/** Every service's inbox route. A user who cannot read a module is refused its source, which is not an error. */
export const INBOX_SOURCES: readonly { name: string; url: string }[] = [
  { name: 'Purchase', url: '/api/purchase/approvals/mine' },
  { name: 'Sales', url: '/api/sales/approvals/mine' },
  { name: 'Journals', url: '/api/journals/approvals/mine' },
  { name: 'Spend money', url: '/api/spend-money/approvals/mine' },
  { name: 'Stock adjustments', url: '/api/stock-adjustments/approvals/mine' },
];

const KIND_LABELS: Record<string, string> = {
  PurchaseOrder: 'Purchase order',
  PurchaseBill: 'Bill',
  DebitNote: 'Debit note',
  SpendMoney: 'Spend money',
  ManualJournal: 'Manual journal',
  CreditNote: 'Credit note',
  SalesDiscountOverride: 'Discount override',
  CreditLimitOverride: 'Credit limit override',
  StockAdjustment: 'Stock adjustment',
};

export function kindLabel(requestKind: string): string {
  return KIND_LABELS[requestKind] ?? requestKind;
}

/** Where the approver's move is posted: the same route the document's own approval panel uses. */
export function actionUrl(item: InboxItem): string {
  switch (item.service) {
    case 'purchase':
      return `/api/purchase/${item.document}/${item.requestId}/approval`;
    case 'accounting':
      return `/api/${item.document}/${item.requestId}/approval`;
    case 'inventory':
      return `/api/stock-adjustments/${item.requestId}/approval`;
    case 'sales':
      if (item.requestKind === 'CreditLimitOverride') {
        return `/api/sales/${item.document}/${item.requestId}/overrides/credit-limit/approval`;
      }
      if (item.requestKind === 'SalesDiscountOverride') {
        return `/api/sales/${item.document}/${item.requestId}/overrides/discount/approval`;
      }
      return `/api/sales/${item.document}/${item.requestId}/approval`;
    default:
      return `/api/${item.document}/${item.requestId}/approval`;
  }
}

/** The screen the document opens on, to read it before deciding. */
export function documentRoute(item: InboxItem): string {
  switch (item.service) {
    case 'purchase':
      return `/purchase/${item.document}/${item.requestId}`;
    case 'sales':
      return `/sales/${item.document}/${item.requestId}`;
    case 'accounting':
      return item.document === 'journals' ? `/accounting/journals/${item.requestId}` : '/banking/spend-money';
    case 'inventory':
      return '/inventory/stock-adjustments';
    default:
      return '/dashboard';
  }
}

/** A stable key for one waiting request across every service. */
export function itemKey(item: InboxItem): string {
  return `${item.service}:${item.document}:${item.requestKind}:${item.requestId}`;
}

/** The merged inbox, oldest first, so what has waited longest is on top. */
export function mergeInbox(lists: readonly (readonly InboxItem[])[]): InboxItem[] {
  return lists
    .flat()
    .slice()
    .sort((a, b) => a.documentDate.localeCompare(b.documentDate) || itemKey(a).localeCompare(itemKey(b)));
}
