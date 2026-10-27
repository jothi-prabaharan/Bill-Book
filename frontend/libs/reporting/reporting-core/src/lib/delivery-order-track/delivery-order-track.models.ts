/**
 * The Delivery Order Track Report's contract (TK-232), field for field with
 * `DeliveryOrderTrackReportModels.cs`, and the pure steps the page takes from an answer: each delivery
 * challan dated in a period with how far it has been billed, its lines with what was delivered and what
 * invoices raised from the challan have billed of each, and what is still to bill. Quantities are in each
 * line's own unit; values are in the branch's base currency, before tax, at the challan's own price per
 * unit, so delivered less invoiced is what is to bill on every line. Dates are ISO `yyyy-MM-dd`.
 */

/** How far a challan has been billed, worked out from every line; void is a withdrawn draft. */
export type DeliveryChallanProgress = 'NotInvoiced' | 'PartlyInvoiced' | 'Invoiced' | 'Void';

/** Why the goods left on a challan; only a sale is a supply. */
export type DeliveryChallanType = 'Sale' | 'JobWork' | 'Approval' | 'BranchTransfer' | 'Sample';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface DeliveryOrderTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only challans at this status; every status but void when blank. */
  progress?: DeliveryChallanProgress | null;
  /** Only challans of this type; every type when blank. */
  challanType?: DeliveryChallanType | null;
  /** Only challans with something still to bill: not invoiced, or partly invoiced. */
  notInvoicedOnly?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

/** One line of a challan. */
export interface DeliveryOrderTrackLine {
  lineNumber: number;
  itemId: number | null;
  /** Blank on a line that is not for an item. */
  itemCode: string | null;
  /** The item's name, else the line's own description. */
  itemName: string;
  unitCode: string | null;
  delivered: number;
  invoiced: number;
  /** Delivered less invoiced. */
  toBill: number;
  deliveredValue: number;
  invoicedValue: number;
  toBillValue: number;
}

/** An invoice raised from a challan. */
export interface DeliveryOrderTrackInvoice {
  /** INV, or POS for a sale rung up at the till. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
}

/** A challan with its lines, the order it was delivered against and the invoices raised from it. */
export interface DeliveryOrderTrackChallan {
  deliveryChallanId: number;
  documentNo: string;
  documentDate: string;
  /** The day the goods went out. */
  dispatchDate: string;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  challanType: DeliveryChallanType;
  progress: DeliveryChallanProgress;
  /** The sales order it was delivered against; null when there is none. */
  salesOrderId: number | null;
  salesOrderNo: string | null;
  lines: DeliveryOrderTrackLine[];
  deliveredValue: number;
  invoicedValue: number;
  toBillValue: number;
  /** The posted invoices raised from the challan, earliest first. */
  invoices: DeliveryOrderTrackInvoice[];
}

export interface DeliveryOrderTrackTotals {
  deliveredValue: number;
  invoicedValue: number;
  toBillValue: number;
}

export interface DeliveryOrderTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of challans, newest first. */
  challans: DeliveryOrderTrackChallan[];
  /** How many challans the filters match, all pages. */
  totalChallans: number;
  pageNumber: number;
  pageSize: number;
  /** How many challans are at each status, none left out; void is counted but is not part of All. */
  progressCounts: Record<DeliveryChallanProgress, number>;
  /** Added up over every challan the filters match, not only this page. */
  totals: DeliveryOrderTrackTotals;
}

/** The statuses in the order the tabs show them: how far billing has got, then the withdrawn ones. */
export const DELIVERY_CHALLAN_PROGRESSES: readonly { value: DeliveryChallanProgress; label: string }[] = [
  { value: 'NotInvoiced', label: 'Not invoiced' },
  { value: 'PartlyInvoiced', label: 'Partly invoiced' },
  { value: 'Invoiced', label: 'Invoiced' },
  { value: 'Void', label: 'Void' },
];

/** The challan types the filter offers, with how each reads on the page. */
export const DELIVERY_CHALLAN_TYPES: readonly { value: DeliveryChallanType; label: string }[] = [
  { value: 'Sale', label: 'Sale' },
  { value: 'JobWork', label: 'Job work' },
  { value: 'Approval', label: 'Approval' },
  { value: 'BranchTransfer', label: 'Branch transfer' },
  { value: 'Sample', label: 'Sample' },
];

/** How a status reads on the page. */
export function deliveryChallanProgressLabel(progress: DeliveryChallanProgress): string {
  return DELIVERY_CHALLAN_PROGRESSES.find((p) => p.value === progress)?.label ?? progress;
}

/** How a challan type reads on the page. */
export function deliveryChallanTypeLabel(type: DeliveryChallanType): string {
  return DELIVERY_CHALLAN_TYPES.find((t) => t.value === type)?.label ?? type;
}

/** The colour of a status's tag: invoiced reads as done, partly invoiced as a warning, not invoiced as outlined. */
export function deliveryChallanProgressTag(progress: DeliveryChallanProgress): string {
  switch (progress) {
    case 'Invoiced':
      return 'tag-success';
    case 'PartlyInvoiced':
      return 'tag-warning';
    case 'NotInvoiced':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: a status ('' is All), its label and how many challans are at it. */
export interface DeliveryOrderTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts are
 * the server's, because a page cannot count the pages it was not sent. All is every status but void, the
 * list a person gets without choosing one.
 */
export function deliveryOrderTrackTabs(counts: Readonly<Partial<Record<DeliveryChallanProgress, number>>> | undefined): DeliveryOrderTrackTab[] {
  const count = (progress: DeliveryChallanProgress): number => counts?.[progress] ?? 0;
  const all = DELIVERY_CHALLAN_PROGRESSES.filter((p) => p.value !== 'Void').reduce((sum, p) => sum + count(p.value), 0);

  return [{ value: '', label: 'All', count: all }, ...DELIVERY_CHALLAN_PROGRESSES.map((p) => ({ ...p, count: count(p.value) }))];
}

/** Whether a challan is in a currency other than the branch's, so its own currency is worth naming. */
export function isForeignChallan(challan: Pick<DeliveryOrderTrackChallan, 'currencyCode'>, baseCurrency: string): boolean {
  return challan.currencyCode !== '' && baseCurrency !== '' && challan.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/** Where a challan opens. */
export function challanLink(deliveryChallanId: number): string {
  return `/sales/delivery-challans/${deliveryChallanId}`;
}

/** Where an invoice raised from a challan opens. */
export function challanInvoiceLink(invoice: Pick<DeliveryOrderTrackInvoice, 'documentId'>): string {
  return `/sales/invoices/${invoice.documentId}`;
}

export type DeliveryOrderTrackRowKind = 'challan' | 'line' | 'subtotal' | 'total';

/** One row of the grid: a challan's heading, one of its lines, its value, or the grand total. */
export interface DeliveryOrderTrackGridRow {
  kind: DeliveryOrderTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** A challan's number, an item's name, or the label of a value or total row. */
  name: string;
  /** Where a challan number opens; null off a heading. */
  link: string | null;
  /** An item's code. */
  code: string | null;
  date: string | null;
  dispatchDate: string | null;
  contactName: string;
  /** The challan's currency when it is not the branch's; else null. */
  currency: string | null;
  challanType: DeliveryChallanType | null;
  progress: DeliveryChallanProgress | null;
  /** The sales order it was delivered against, and where that opens; on a heading. */
  orderNo: string | null;
  orderLink: string | null;
  /** The invoices raised from it, each with where it opens; on a heading. */
  invoices: { no: string; link: string }[];
  unitCode: string | null;
  delivered: number | null;
  invoiced: number | null;
  toBill: number | null;
  deliveredValue: number | null;
  invoicedValue: number | null;
  toBillValue: number | null;
}

const blank = (kind: DeliveryOrderTrackRowKind, key: string, name: string): DeliveryOrderTrackGridRow => ({
  kind,
  key,
  name,
  link: null,
  code: null,
  date: null,
  dispatchDate: null,
  contactName: '',
  currency: null,
  challanType: null,
  progress: null,
  orderNo: null,
  orderLink: null,
  invoices: [],
  unitCode: null,
  delivered: null,
  invoiced: null,
  toBill: null,
  deliveredValue: null,
  invoicedValue: null,
  toBillValue: null,
});

/**
 * The grid's rows: for each challan a heading, its lines and its value; then the total of every challan
 * the filters match, which is the server's and not the loaded challans' sum, so it stays right until the
 * last page is loaded. Nothing is worked out here beyond the layout.
 */
export function deliveryOrderTrackRows(result: DeliveryOrderTrackResult): DeliveryOrderTrackGridRow[] {
  const rows: DeliveryOrderTrackGridRow[] = [];

  for (const challan of result.challans) {
    const id = `c:${challan.deliveryChallanId}`;

    rows.push({
      ...blank('challan', id, challan.documentNo),
      link: challanLink(challan.deliveryChallanId),
      date: challan.documentDate,
      dispatchDate: challan.dispatchDate,
      contactName: challan.contactName,
      currency: isForeignChallan(challan, result.baseCurrencyCode) ? challan.currencyCode : null,
      challanType: challan.challanType,
      progress: challan.progress,
      orderNo: challan.salesOrderNo,
      orderLink: challan.salesOrderId === null ? null : `/sales/sales-orders/${challan.salesOrderId}`,
      invoices: challan.invoices.map((i) => ({ no: i.documentNo, link: challanInvoiceLink(i) })),
    });

    for (const line of challan.lines) {
      rows.push({
        ...blank('line', `${id}:${line.lineNumber}`, line.itemName),
        code: line.itemCode,
        unitCode: line.unitCode,
        delivered: line.delivered,
        invoiced: line.invoiced,
        toBill: line.toBill,
        deliveredValue: line.deliveredValue,
        invoicedValue: line.invoicedValue,
        toBillValue: line.toBillValue,
      });
    }

    rows.push({
      ...blank('subtotal', `${id}:v`, 'Challan value'),
      deliveredValue: challan.deliveredValue,
      invoicedValue: challan.invoicedValue,
      toBillValue: challan.toBillValue,
    });
  }

  if (result.challans.length > 0) {
    const t = result.totals;
    rows.push({
      ...blank('total', 't', result.challans.length < result.totalChallans ? `Total of all ${result.totalChallans} challans` : 'Total'),
      deliveredValue: t.deliveredValue,
      invoicedValue: t.invoicedValue,
      toBillValue: t.toBillValue,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendDeliveryOrderTrackPage(loaded: DeliveryOrderTrackResult, next: DeliveryOrderTrackResult): DeliveryOrderTrackResult {
  return { ...next, challans: [...loaded.challans, ...next.challans] };
}

/** Whether there are challans the filters match that are not loaded yet. */
export function hasMoreDeliveryOrderTrack(result: Pick<DeliveryOrderTrackResult, 'totalChallans' | 'challans'>): boolean {
  return result.challans.length < result.totalChallans;
}

/**
 * What the period came to, in a sentence: how many challans, what was delivered, and how much of it is
 * invoiced and still to bill. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function deliveryOrderTrackSentence(
  result: Pick<DeliveryOrderTrackResult, 'totalChallans' | 'totals'>,
  money: (value: number) => string,
): string {
  const n = result.totalChallans;
  const what = n === 1 ? '1 challan' : `${n} challans`;
  const t = result.totals;

  return `${what} delivered ${money(t.deliveredValue)}: ${money(t.invoicedValue)} invoiced and ${money(t.toBillValue)} still to bill.`;
}
