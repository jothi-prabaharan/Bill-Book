/**
 * The Aged Payables Details' contract (TK-234), field for field with `AgedPayablesDetailsReportModels.cs`, and
 * the pure steps the page takes from an answer: the Aged Payables Summary one document at a time, what makes up
 * what the branch owes each vendor. Every figure is in the branch's base currency. A debit note, or a payment or
 * advance not yet applied to a bill, is negative and sits in Current. Dates are ISO `yyyy-MM-dd`.
 *
 * The age basis, the column widths and the way a link's query string is read are the receivable reports', and are
 * imported from them rather than declared twice; the vendor list is the summary's.
 */

import { blankZero } from '../journal-report/journal-report.models';
import { AgeBasis } from '../aged-receivables-summary/aged-receivables-summary.models';
import { columnOf, detailOverdue } from '../aged-receivables-detail/aged-receivables-detail.models';

/** What a document is, told from the code its postings carry. */
export type PayableDocumentKind = 'Bill' | 'DebitNote' | 'Payment' | 'Receipt' | 'Journal' | 'OpeningBalance' | 'Other';

/** What the report is asked for. The as-at date is required; the rest narrow or change it. */
export interface AgedPayablesDetailsRequest {
  asAtDate: string;
  /** Due date when blank; invoice date ages a bill from its bill date. */
  ageBy?: AgeBasis | null;
  /** 30 when blank. */
  bucketDays?: number | null;
  /** Only this vendor; every vendor when blank. */
  contactId?: number | null;
}

/** One document still open on the date. */
export interface AgedPayablesDetailsDocument {
  kind: PayableDocumentKind;
  /** The three-letter code its postings carry, such as BIL, DBN or SPM. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  /** Only a bill has one. */
  dueDate: string | null;
  /** How far past the date it is counted from the as-at date is; null for a debit and for what is not yet due. */
  daysOverdue: number | null;
  /** What it came to before anything was applied, signed as the outstanding amount is. */
  original: number;
  settled: number;
  outstanding: number;
  current: number;
  bucket1: number;
  bucket2: number;
  bucket3: number;
  bucket4: number;
}

/** A vendor's documents, or all of them, added up. */
export interface AgedPayablesDetailsTotals {
  original: number;
  settled: number;
  outstanding: number;
  current: number;
  bucket1: number;
  bucket2: number;
  bucket3: number;
  bucket4: number;
}

export interface AgedPayablesDetailsVendor {
  /** Null for what no vendor claims. */
  contactId: number | null;
  contactCode: string;
  contactName: string;
  /** A micro or small enterprise, whose bills the Sec 43B(h) 45-day rule applies to. */
  isMsme: boolean;
  documents: AgedPayablesDetailsDocument[];
  subtotal: AgedPayablesDetailsTotals;
}

export interface AgedPayablesDetailsResult {
  asAtDate: string;
  ageBy: AgeBasis;
  bucketDays: number;
  baseCurrencyCode: string;
  /** The five column headings, Current first. */
  bucketLabels: string[];
  vendors: AgedPayablesDetailsVendor[];
  /** Every document added up: the Accounts Payable balance on the date. */
  totals: AgedPayablesDetailsTotals;
}

const KIND_LABELS: Record<PayableDocumentKind, string> = {
  Bill: 'Bill',
  DebitNote: 'Debit note',
  Payment: 'Payment',
  Receipt: 'Receipt',
  Journal: 'Journal',
  OpeningBalance: 'Opening balance',
  Other: 'Other',
};

/** How a kind of document reads on the page. */
export function payableKindLabel(kind: PayableDocumentKind): string {
  return KIND_LABELS[kind] ?? kind;
}

/**
 * Where a document can be opened, or null when it has no page of its own: a bill, a debit note and a manual
 * journal do. A payment or receipt is only in its register, so it is not linked rather than linked to a list.
 */
export function payableDocumentLink(document: Pick<AgedPayablesDetailsDocument, 'typeCode' | 'documentId'>): string | null {
  switch (document.typeCode) {
    case 'BIL':
      return `/purchase/bills/${document.documentId}`;
    case 'DBN':
      return `/purchase/debit-notes/${document.documentId}`;
    case 'JRN':
      return `/accounting/journals/${document.documentId}`;
    default:
      return null;
  }
}

export type AgedPayablesDetailsRowKind = 'heading' | 'document' | 'subtotal' | 'total';

/** One row of the grid: a vendor's heading, a document, a vendor's subtotal, or the totals. */
export interface AgedPayablesDetailsGridRow {
  kind: AgedPayablesDetailsRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The vendor on a heading, the number on a document, the label on a subtotal. */
  name: string;
  /** The vendor's code on a heading, the kind of document on a document; empty elsewhere. */
  detail: string;
  /** On a heading: whether the vendor is an MSME. */
  isMsme: boolean;
  link: string | null;
  documentDate: string | null;
  dueDate: string | null;
  daysOverdue: number | null;
  /** Null on a heading, where nothing applies, and a nought on a document so the cell reads blank. */
  original: number | null;
  settled: number | null;
  outstanding: number | null;
  current: number | null;
  bucket1: number | null;
  bucket2: number | null;
  bucket3: number | null;
  bucket4: number | null;
  /** The column a document's amount sits in; null on every other row. */
  column: number | null;
}

const blank = (): Pick<
  AgedPayablesDetailsGridRow,
  'documentDate' | 'dueDate' | 'daysOverdue' | 'original' | 'settled' | 'outstanding' | 'current' | 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4' | 'column'
> => ({
  documentDate: null,
  dueDate: null,
  daysOverdue: null,
  original: null,
  settled: null,
  outstanding: null,
  current: null,
  bucket1: null,
  bucket2: null,
  bucket3: null,
  bucket4: null,
  column: null,
});

function totalsRow(kind: 'subtotal' | 'total', key: string, name: string, t: AgedPayablesDetailsTotals): AgedPayablesDetailsGridRow {
  return {
    kind,
    key,
    name,
    detail: '',
    isMsme: false,
    link: null,
    ...blank(),
    original: t.original,
    settled: t.settled,
    outstanding: t.outstanding,
    current: t.current,
    bucket1: t.bucket1,
    bucket2: t.bucket2,
    bucket3: t.bucket3,
    bucket4: t.bucket4,
  };
}

/**
 * The grid's rows, vendor by vendor as the server ordered them: a heading, its documents oldest first, then its
 * subtotal; and the totals when there is more than one vendor to total. A vendor with one document has no subtotal,
 * since it would repeat the line above it. Nothing is worked out here beyond the layout; the figures are the server's.
 */
export function agedPayablesDetailsRows(result: AgedPayablesDetailsResult): AgedPayablesDetailsGridRow[] {
  const rows: AgedPayablesDetailsGridRow[] = [];

  for (const vendor of result.vendors) {
    const id = vendor.contactId === null ? 'u' : `v${vendor.contactId}`;

    rows.push({
      kind: 'heading',
      key: `h:${id}`,
      name: vendor.contactName,
      detail: vendor.contactCode,
      isMsme: vendor.isMsme,
      link: null,
      ...blank(),
    });

    for (const d of vendor.documents) {
      rows.push({
        kind: 'document',
        key: `d:${id}:${d.typeCode}:${d.documentId}`,
        name: d.documentNo,
        detail: payableKindLabel(d.kind),
        isMsme: false,
        link: payableDocumentLink(d),
        documentDate: d.documentDate,
        dueDate: d.dueDate,
        daysOverdue: d.daysOverdue,
        original: d.original,
        settled: blankZero(d.settled),
        outstanding: d.outstanding,
        current: blankZero(d.current),
        bucket1: blankZero(d.bucket1),
        bucket2: blankZero(d.bucket2),
        bucket3: blankZero(d.bucket3),
        bucket4: blankZero(d.bucket4),
        column: columnOf(d),
      });
    }

    if (vendor.documents.length > 1) {
      rows.push(totalsRow('subtotal', `s:${id}`, `Total ${vendor.contactName}`, vendor.subtotal));
    }
  }

  // With one vendor the totals would repeat its subtotal, or its only document, so they are left off.
  if (result.vendors.length > 1) {
    rows.push(totalsRow('total', 't', 'Total', result.totals));
  }

  return rows;
}

/**
 * What the report came to, in a sentence: what the branch owes, how many documents and vendors make it up and how
 * much is past due. Money is formatted by the caller so the sentence reads in the branch's own currency.
 */
export function agedPayablesDetailsSentence(
  result: Pick<AgedPayablesDetailsResult, 'totals' | 'vendors'>,
  money: (value: number) => string,
): string {
  const documents = result.vendors.reduce((n, v) => n + v.documents.length, 0);
  const vendors = result.vendors.filter((v) => v.contactId !== null).length;
  const what = `${documents} open ${documents === 1 ? 'document' : 'documents'}`;
  const who = vendors === 1 ? '1 vendor' : `${vendors} vendors`;
  const overdue = detailOverdue(result.totals);

  return overdue > 0
    ? `${money(result.totals.outstanding)} is owed on ${what} across ${who}; ${money(overdue)} of bills is overdue.`
    : `${money(result.totals.outstanding)} is owed on ${what} across ${who}; nothing is overdue.`;
}
