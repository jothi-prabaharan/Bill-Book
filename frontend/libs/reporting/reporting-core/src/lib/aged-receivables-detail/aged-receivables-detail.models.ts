/**
 * The Aged Receivables Detail's contract (TK-223), field for field with
 * `AgedReceivablesDetailReportModels.cs`, and the pure steps the page takes from an answer:
 * the Aged Receivables Summary one document at a time, what makes up each customer's balance.
 * Every figure is in the branch's base currency. A credit note, or a receipt or advance not yet
 * applied to an invoice, is negative and sits in Current. Dates are ISO `yyyy-MM-dd`.
 *
 * The age basis, the column widths, the customer list and the customer choice are the
 * summary's, and are imported from it rather than declared twice.
 */

import { blankZero } from '../journal-report/journal-report.models';
import { AGE_BASIS_OPTIONS, AgeBasis, BUCKET_SIZES } from '../aged-receivables-summary/aged-receivables-summary.models';

/** What a document is, told from the code its postings carry. */
export type ReceivableDocumentKind = 'Invoice' | 'CreditNote' | 'Receipt' | 'Payment' | 'Journal' | 'OpeningBalance' | 'Other';

/** What the report is asked for. The as-at date is required; the rest narrow or change it. */
export interface AgedReceivablesDetailRequest {
  asAtDate: string;
  /** Due date when blank. */
  ageBy?: AgeBasis | null;
  /** 30 when blank. */
  bucketDays?: number | null;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
}

/** One document still open on the date. */
export interface AgedReceivablesDetailDocument {
  kind: ReceivableDocumentKind;
  /** The three-letter code its postings carry, such as INV, CRN or RCM. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
  /** Only an invoice has one. */
  dueDate: string | null;
  /** How far past the date it is counted from the as-at date is; null for a credit and for what is not yet due. */
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

/** A customer's documents, or all of them, added up. */
export interface AgedReceivablesDetailTotals {
  original: number;
  settled: number;
  outstanding: number;
  current: number;
  bucket1: number;
  bucket2: number;
  bucket3: number;
  bucket4: number;
}

export interface AgedReceivablesDetailCustomer {
  /** Null for what no customer claims. */
  contactId: number | null;
  contactCode: string;
  contactName: string;
  documents: AgedReceivablesDetailDocument[];
  subtotal: AgedReceivablesDetailTotals;
}

export interface AgedReceivablesDetailResult {
  asAtDate: string;
  ageBy: AgeBasis;
  bucketDays: number;
  baseCurrencyCode: string;
  /** The five column headings, Current first. */
  bucketLabels: string[];
  customers: AgedReceivablesDetailCustomer[];
  /** Every document added up: the Accounts Receivable balance on the date. */
  totals: AgedReceivablesDetailTotals;
}

const KIND_LABELS: Record<ReceivableDocumentKind, string> = {
  Invoice: 'Invoice',
  CreditNote: 'Credit note',
  Receipt: 'Receipt',
  Payment: 'Payment',
  Journal: 'Journal',
  OpeningBalance: 'Opening balance',
  Other: 'Other',
};

/** How a kind of document reads on the page. */
export function documentKindLabel(kind: ReceivableDocumentKind): string {
  return KIND_LABELS[kind] ?? kind;
}

/**
 * Where a document can be opened, or null when it has no page of its own: an invoice (a POS
 * sale included), a credit note and a manual journal do. A receipt or payment is only in its
 * register, so it is not linked rather than linked to a list.
 */
export function documentLink(document: Pick<AgedReceivablesDetailDocument, 'typeCode' | 'documentId'>): string | null {
  switch (document.typeCode) {
    case 'INV':
    case 'POS':
      return `/sales/invoices/${document.documentId}`;
    case 'CRN':
      return `/sales/credit-notes/${document.documentId}`;
    case 'JRN':
      return `/accounting/journals/${document.documentId}`;
    default:
      return null;
  }
}

/**
 * Which of the five columns a document's amount sits in, 0 for Current to 4 for the last. A
 * document has one amount, so it is the first column that is not nought; a nought document
 * (which the server does not send) is Current.
 */
export function columnOf(document: Pick<AgedReceivablesDetailDocument, 'current' | 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4'>): number {
  const index = [document.current, document.bucket1, document.bucket2, document.bucket3, document.bucket4].findIndex((v) => v !== 0);
  return index < 0 ? 0 : index;
}

export type AgedReceivablesDetailRowKind = 'heading' | 'document' | 'subtotal' | 'total';

/** One row of the grid: a customer's heading, a document, a customer's subtotal, or the totals. */
export interface AgedReceivablesDetailGridRow {
  kind: AgedReceivablesDetailRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The customer on a heading, the number on a document, the label on a subtotal. */
  name: string;
  /** The customer's code on a heading, the kind of document on a document; empty elsewhere. */
  detail: string;
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
  AgedReceivablesDetailGridRow,
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

function totalsRow(kind: 'subtotal' | 'total', key: string, name: string, t: AgedReceivablesDetailTotals): AgedReceivablesDetailGridRow {
  return {
    kind,
    key,
    name,
    detail: '',
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
 * The grid's rows, customer by customer as the server ordered them: a heading, its documents
 * oldest first, then its subtotal; and the totals when there is more than one customer to total.
 * A customer with one document has no subtotal, since it would repeat the line above it. Nothing
 * is worked out here beyond the layout; the figures are the server's.
 */
export function agedReceivablesDetailRows(result: AgedReceivablesDetailResult): AgedReceivablesDetailGridRow[] {
  const rows: AgedReceivablesDetailGridRow[] = [];

  for (const customer of result.customers) {
    const id = customer.contactId === null ? 'u' : `c${customer.contactId}`;

    rows.push({
      kind: 'heading',
      key: `h:${id}`,
      name: customer.contactName,
      detail: customer.contactCode,
      link: null,
      ...blank(),
    });

    for (const d of customer.documents) {
      rows.push({
        kind: 'document',
        key: `d:${id}:${d.typeCode}:${d.documentId}`,
        name: d.documentNo,
        detail: documentKindLabel(d.kind),
        link: documentLink(d),
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

    if (customer.documents.length > 1) {
      rows.push(totalsRow('subtotal', `s:${id}`, `Total ${customer.contactName}`, customer.subtotal));
    }
  }

  // With one customer the totals would repeat its subtotal, or its only document, so they are left off.
  if (result.customers.length > 1) {
    rows.push(totalsRow('total', 't', 'Total', result.totals));
  }

  return rows;
}

/** What is past due: the four overdue columns added up. A credit sitting in Current is not taken off it. */
export function detailOverdue(totals: Pick<AgedReceivablesDetailTotals, 'bucket1' | 'bucket2' | 'bucket3' | 'bucket4'>): number {
  return totals.bucket1 + totals.bucket2 + totals.bucket3 + totals.bucket4;
}

/**
 * What the report came to, in a sentence: the balance, how many documents and customers make it
 * up and how much is past due. Money is formatted by the caller so the sentence reads in the
 * branch's own currency.
 */
export function agedReceivablesDetailSentence(
  result: Pick<AgedReceivablesDetailResult, 'totals' | 'customers'>,
  money: (value: number) => string,
): string {
  const documents = result.customers.reduce((n, c) => n + c.documents.length, 0);
  const customers = result.customers.filter((c) => c.contactId !== null).length;
  const what = `${documents} open ${documents === 1 ? 'document' : 'documents'}`;
  const who = customers === 1 ? '1 customer' : `${customers} customers`;
  const overdue = detailOverdue(result.totals);

  return overdue > 0
    ? `${money(result.totals.outstanding)} is owed on ${what} across ${who}; ${money(overdue)} of invoices is overdue.`
    : `${money(result.totals.outstanding)} is owed on ${what} across ${who}; nothing is overdue.`;
}

/** What the page starts from when it is opened with a query string, as the summary's links send it. */
export interface AgedReceivablesDetailQuery {
  asAtDate: string | null;
  ageBy: AgeBasis | null;
  bucketDays: number | null;
  contactId: number | null;
}

/**
 * The filters a query string asks for. Anything that is not a real value of its filter (a date that
 * is not `yyyy-MM-dd`, a basis or width the report does not offer, a customer that is not a whole
 * number above nought) is dropped, so a hand-edited address starts the page on its defaults instead
 * of on a refusal.
 */
export function detailQueryFrom(get: (name: string) => string | null): AgedReceivablesDetailQuery {
  const date = get('asAtDate');
  const basis = AGE_BASIS_OPTIONS.find((o) => o.value === get('ageBy'))?.value ?? null;
  const width = Number(get('bucketDays'));
  const customer = Number(get('contactId'));

  return {
    asAtDate: date !== null && /^\d{4}-\d{2}-\d{2}$/.test(date) && !Number.isNaN(Date.parse(date)) ? date : null,
    ageBy: basis,
    bucketDays: BUCKET_SIZES.includes(width) ? width : null,
    contactId: Number.isInteger(customer) && customer > 0 ? customer : null,
  };
}
