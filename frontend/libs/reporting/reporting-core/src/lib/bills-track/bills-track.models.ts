/**
 * The Bills Track Report's contract (TK-237), field for field with `BillsTrackReportModels.cs`, and
 * the pure steps the page takes from an answer: where each vendor bill dated in a period is in its
 * life, with the dates of its steps, what is still owed to the vendor and, where the bill went
 * through an approval chain, where that stands. Amounts are in the branch's base currency; a bill in
 * another currency also carries its own total. Paid and due are as at the To date. A draft is not in
 * the books, so it owes nothing and is left out of the totals. Dates are ISO `yyyy-MM-dd`; the
 * posting moment is an ISO timestamp.
 */

import { payableBillLink } from '../payable-invoice-summary/payable-invoice-summary.models';
import { localIsoDate } from '../invoice-track/invoice-track.models';

/** Where a bill stands as at the To date; overdue wins over partly paid. */
export type BillTrackStatus = 'Draft' | 'ReadyToPost' | 'AwaitingPayment' | 'PartlyPaid' | 'Overdue' | 'Paid' | 'Void';

/** Where a bill stands in an approval chain; absent when no workflow was involved. */
export type BillApprovalStatus = 'Draft' | 'InApproval' | 'Approved' | 'Rejected';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface BillsTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
  /** Only this status; every status but void when blank. */
  status?: BillTrackStatus | null;
  pageNumber?: number;
  pageSize?: number;
}

/** One bill with the dates of its life. */
export interface BillsTrackRow {
  /** BIL. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  /** The vendor's own number for the bill. */
  vendorBillNo: string | null;
  documentDate: string;
  /** When it was posted; null for a draft and a ready-to-post bill. */
  postedAt: string | null;
  /** When it falls due; null for a bill not yet in the books. */
  dueDate: string | null;
  /** The latest settlement of it on or before the To date; null when nothing was settled. */
  lastPaymentDate: string | null;
  /** How far past its due date it was on the To date; null unless it is overdue. */
  daysOverdue: number | null;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The tax is the branch's own to pay, not the vendor's. */
  reverseCharge: boolean;
  /** The total in the bill's own currency. */
  totalInCurrency: number;
  total: number;
  /** What the vendor is owed on it: the total less TDS and, on a reverse-charge bill, the tax. */
  payable: number;
  paid: number;
  due: number;
  status: BillTrackStatus;
  /** Where its approval stands; null when no workflow was involved. */
  approvalStatus: BillApprovalStatus | null;
  /** The level an in-approval bill waits on. */
  approvalStep: string | null;
}

/** The posted bills the filters match that still stand, added up: not a draft, not a voided one. */
export interface BillsTrackTotals {
  total: number;
  paid: number;
  due: number;
}

export interface BillsTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of bills, newest first. */
  bills: BillsTrackRow[];
  /** How many the filters match, all pages. */
  totalBills: number;
  pageNumber: number;
  pageSize: number;
  /** How many bills have each status, none left out; void is counted but is not part of All. */
  statusCounts: Record<BillTrackStatus, number>;
  /** Added up over every posted bill the filters match, not only this page. */
  totals: BillsTrackTotals;
}

/** The statuses in the order the tabs show them: the life of a bill, start to end. */
export const BILL_TRACK_STATUSES: readonly { value: BillTrackStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'ReadyToPost', label: 'Ready to post' },
  { value: 'AwaitingPayment', label: 'Awaiting payment' },
  { value: 'PartlyPaid', label: 'Partly paid' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Paid', label: 'Paid' },
  { value: 'Void', label: 'Void' },
];

/** How a status reads on the page. */
export function billTrackStatusLabel(status: BillTrackStatus): string {
  return BILL_TRACK_STATUSES.find((s) => s.value === status)?.label ?? status;
}

/** The colour of a status's tag: paid reads as done, overdue as a problem, and what is not yet in the books as outlined. */
export function billTrackStatusTag(status: BillTrackStatus): string {
  switch (status) {
    case 'Paid':
      return 'tag-success';
    case 'Overdue':
      return 'tag-danger';
    case 'PartlyPaid':
      return 'tag-warning';
    case 'AwaitingPayment':
      return 'tag-info';
    case 'Draft':
    case 'ReadyToPost':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/**
 * How a bill's approval reads, or null when there is nothing to say: no workflow was involved, or the
 * bill has not been submitted. An in-approval bill says which level it waits on.
 */
export function billApprovalLabel(status: BillApprovalStatus | null, step: string | null): string | null {
  switch (status) {
    case 'InApproval':
      return step ? `In approval · ${step}` : 'In approval';
    case 'Approved':
      return 'Approved';
    case 'Rejected':
      return 'Rejected';
    default:
      return null;
  }
}

/** The colour of an approval's tag: approved reads as done, rejected as a problem, waiting as pending. */
export function billApprovalTag(status: BillApprovalStatus | null): string {
  switch (status) {
    case 'Approved':
      return 'tag-success';
    case 'Rejected':
      return 'tag-danger';
    default:
      return 'tag-warning';
  }
}

/** A tab above the grid: a status ('' is All), its label and how many bills have it. */
export interface BillsTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every status, a nought included so a tab can say nothing is in it. The counts
 * are the server's, because a page cannot count the pages it was not sent. All is every status but
 * void, the list a person gets without choosing one.
 */
export function billsTrackTabs(counts: Readonly<Partial<Record<BillTrackStatus, number>>> | undefined): BillsTrackTab[] {
  const count = (status: BillTrackStatus): number => counts?.[status] ?? 0;
  const all = BILL_TRACK_STATUSES.filter((s) => s.value !== 'Void').reduce((sum, s) => sum + count(s.value), 0);

  return [{ value: '', label: 'All', count: all }, ...BILL_TRACK_STATUSES.map((s) => ({ ...s, count: count(s.value) }))];
}

/** Whether a bill is in the books: posted or voided, as against a draft or one ready to post. */
export function billInBooks(status: BillTrackStatus): boolean {
  return status !== 'Draft' && status !== 'ReadyToPost';
}

/** Whether a bill is in a currency other than the branch's, so its own total is worth showing. */
export function isForeignBillTrack(row: Pick<BillsTrackRow, 'currencyCode'>, baseCurrency: string): boolean {
  return row.currencyCode !== '' && baseCurrency !== '' && row.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

export type BillsTrackRowKind = 'bill' | 'total';

/** One row of the grid: a bill, or the totals of every posted bill the filters match that still stands. */
export interface BillsTrackGridRow {
  kind: BillsTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The bill's number, or the label on the totals row. */
  name: string;
  /** Where the number opens; null on the totals row. */
  link: string | null;
  vendorBillNo: string | null;
  documentDate: string | null;
  /** The day it was posted, in the viewer's time; null while it is not in the books. */
  postedOn: string | null;
  dueDate: string | null;
  lastPaymentDate: string | null;
  daysOverdue: number | null;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  reverseCharge: boolean;
  /** The bill's own total when it is in another currency, else null. */
  ownTotal: number | null;
  total: number;
  /** What the vendor is owed, shown under the total when it is not the total (TDS withheld, or reverse charge); else null. */
  owed: number | null;
  paid: number;
  due: number;
  /** A draft and a ready-to-post bill owe nothing yet, so their paid and due are left blank. */
  inBooks: boolean;
  status: BillTrackStatus | null;
  /** How its approval reads, when a workflow was involved; else null. */
  approval: string | null;
  approvalTag: string;
}

/**
 * The grid's rows: each bill as the server sent it, then the totals of every posted bill the filters
 * match, which are the server's and not the loaded rows' sum, so they stay right until the last page
 * is loaded. Nothing is worked out here beyond the layout and the day of the posting.
 */
export function billsTrackRows(result: BillsTrackResult): BillsTrackGridRow[] {
  const rows: BillsTrackGridRow[] = result.bills.map((r) => {
    const inBooks = billInBooks(r.status);

    return {
      kind: 'bill',
      key: `${r.typeCode}:${r.documentId}`,
      name: r.documentNo,
      link: payableBillLink(r),
      vendorBillNo: r.vendorBillNo,
      documentDate: r.documentDate,
      postedOn: localIsoDate(r.postedAt),
      dueDate: r.dueDate,
      lastPaymentDate: r.lastPaymentDate,
      daysOverdue: r.daysOverdue,
      contactCode: r.contactCode,
      contactName: r.contactName,
      currencyCode: r.currencyCode,
      reverseCharge: r.reverseCharge,
      ownTotal: isForeignBillTrack(r, result.baseCurrencyCode) ? r.totalInCurrency : null,
      total: r.total,
      owed: inBooks && r.status !== 'Void' && r.payable !== r.total ? r.payable : null,
      paid: r.paid,
      due: r.due,
      inBooks,
      status: r.status,
      approval: billApprovalLabel(r.approvalStatus, r.approvalStep),
      approvalTag: billApprovalTag(r.approvalStatus),
    };
  });

  if (result.bills.length > 0) {
    const t = result.totals;
    rows.push({
      kind: 'total',
      key: 't',
      name: result.bills.length < result.totalBills ? `Total of posted, all ${result.totalBills}` : 'Total of posted',
      link: null,
      vendorBillNo: null,
      documentDate: null,
      postedOn: null,
      dueDate: null,
      lastPaymentDate: null,
      daysOverdue: null,
      contactCode: '',
      contactName: '',
      currencyCode: '',
      reverseCharge: false,
      ownTotal: null,
      total: t.total,
      owed: null,
      paid: t.paid,
      due: t.due,
      inBooks: true,
      status: null,
      approval: null,
      approvalTag: '',
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts and totals are the latest answer's. */
export function appendBillsTrackPage(loaded: BillsTrackResult, next: BillsTrackResult): BillsTrackResult {
  return { ...next, bills: [...loaded.bills, ...next.bills] };
}

/** Whether there are bills the filters match that are not loaded yet. */
export function hasMoreBillsTrack(result: Pick<BillsTrackResult, 'totalBills' | 'bills'>): boolean {
  return result.bills.length < result.totalBills;
}

/**
 * What the period came to, in a sentence: how many bills, and what the posted ones are for and how
 * much of it is paid and still owed. Money is formatted by the caller so it reads in the branch's own currency.
 */
export function billsTrackSentence(result: Pick<BillsTrackResult, 'totalBills' | 'totals'>, money: (value: number) => string): string {
  const n = result.totalBills;
  const what = n === 1 ? '1 bill' : `${n} bills`;
  const t = result.totals;

  return `${what}. Posted: ${money(t.total)}, of which ${money(t.paid)} is paid and ${money(t.due)} is still owed to vendors.`;
}
