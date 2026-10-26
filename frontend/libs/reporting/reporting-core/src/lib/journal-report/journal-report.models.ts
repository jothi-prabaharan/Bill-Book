/**
 * The Journal Report's contract (TK-217), field for field with
 * `JournalReportModels.cs`, and the pure steps the page takes from an answer.
 * Every journal is a document: a manual journal, or the posting a sales invoice,
 * a bill, a payment or a stock adjustment made to the ledger. Amounts are in the
 * document's own currency and in the branch's base currency. Dates are ISO
 * `yyyy-MM-dd`.
 */

export type JournalStatus = 'Draft' | 'Posted' | 'Reversed';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface JournalReportRequest {
  fromDate: string;
  toDate: string;
  /** The document type that posted it, such as JRN or INV; every source when blank. */
  source?: string | null;
  /** Posted or Reversed, or Draft for journals not yet posted; blank is posted and reversed together. */
  status?: JournalStatus | null;
  /** Only the journals that have a line on this account, each with all its lines. */
  accountId?: number | null;
  /** The page of journals wanted, from 1. */
  pageNumber?: number;
  pageSize?: number;
}

export interface JournalReportLine {
  lineNumber: number;
  accountId: number;
  accountCode: string;
  accountName: string;
  contactId: number | null;
  contactName: string | null;
  description: string | null;
  currencyCode: string;
  /** The rate the document was booked at; never looked up again. */
  exchangeRate: number;
  debitSource: number;
  creditSource: number;
  debit: number;
  credit: number;
}

export interface JournalReportEntry {
  sourceCode: string;
  transactionId: number;
  /** The manual journal behind it; null for a document's own posting. */
  journalId: number | null;
  date: string;
  number: string;
  reference: string | null;
  narration: string | null;
  status: JournalStatus;
  createdByName: string | null;
  createdAt: string | null;
  postedByName: string | null;
  postedAt: string | null;
  reversesNumber: string | null;
  reversedByNumber: string | null;
  lines: JournalReportLine[];
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
}

export interface JournalReportResult {
  fromDate: string;
  toDate: string;
  pageNumber: number;
  pageSize: number;
  /** Every journal the filters match, not only those on this page. */
  totalJournals: number;
  /** Over every journal the filters match; the Trial Balance's movement when nothing narrows it. */
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
  journals: JournalReportEntry[];
}

/** A document type from `mst.TransactionTypes`, for the source filter and the names beside each journal. */
export interface JournalSourceOption {
  code: string;
  name: string;
}

/** What the status filter offers; blank is posted and reversed together, and Draft is only when asked. */
export const JOURNAL_STATUS_CHOICES: readonly { value: JournalStatus; label: string }[] = [
  { value: 'Posted', label: 'Posted' },
  { value: 'Reversed', label: 'Reversed' },
  { value: 'Draft', label: 'Draft' },
];

/** The name of a source, or its code when Master does not name it. */
export function sourceName(code: string, sources: readonly JournalSourceOption[]): string {
  return sources.find((s) => s.code === code)?.name ?? code;
}

/** What a journal says about the one it reverses or was reversed by, or nothing. */
export function reversalNote(entry: Pick<JournalReportEntry, 'reversesNumber' | 'reversedByNumber'>): string | null {
  const notes: string[] = [];
  if (entry.reversesNumber) {
    notes.push(`Reverses ${entry.reversesNumber}`);
  }
  if (entry.reversedByNumber) {
    notes.push(`Reversed by ${entry.reversedByNumber}`);
  }
  return notes.length === 0 ? null : notes.join('. ');
}

/**
 * Who made a journal and when, in a sentence: who created it and who posted it. A
 * document's own posting has one person and one moment, so it says only who
 * posted it; a draft has not been posted, so it says only who created it.
 */
export function auditNote(
  entry: Pick<JournalReportEntry, 'createdByName' | 'createdAt' | 'postedByName' | 'postedAt'>,
  when: (value: string) => string,
): string {
  const hasCreated = !!(entry.createdByName || entry.createdAt);
  const hasPosted = !!(entry.postedByName || entry.postedAt);
  const created = `Created${by(entry.createdByName)}${at(entry.createdAt, when)}`;
  const posted = `Posted${by(entry.postedByName)}${at(entry.postedAt, when)}`;
  const sameMoment = entry.createdAt === entry.postedAt && entry.createdByName === entry.postedByName;

  if (hasPosted && sameMoment) {
    return posted;
  }

  return [hasCreated ? created : '', hasPosted ? posted : ''].filter(Boolean).join('. ');
}

function by(name: string | null): string {
  return name ? ` by ${name}` : '';
}

function at(value: string | null, when: (value: string) => string): string {
  return value ? ` on ${when(value)}` : '';
}

/**
 * A line's amount in its own currency with the rate it was booked at, such as
 * "USD 10.00 @ 83.5", written under the description of a line booked in another currency; empty in the base currency.
 * `amount` writes the figure: the page passes the branch's format (TK-262), and the default is two places.
 */
export function sourceNote(
  line: Pick<JournalReportLine, 'currencyCode' | 'exchangeRate' | 'debitSource' | 'creditSource'>,
  amount: (value: number) => string = (value) => value.toFixed(2),
): string {
  if (line.exchangeRate === 1) {
    return '';
  }

  const figure = line.debitSource !== 0 ? line.debitSource : line.creditSource;
  return `${line.currencyCode} ${amount(figure)} @ ${line.exchangeRate}`;
}

/** An empty side of a line reads blank rather than as a nought. */
export function blankZero(value: number): number | null {
  return value === 0 ? null : value;
}

/** The pages loaded so far with the next one added; the totals are the latest answer's. */
export function appendPage(loaded: JournalReportResult, next: JournalReportResult): JournalReportResult {
  return { ...next, journals: [...loaded.journals, ...next.journals] };
}

/** Whether there are journals the filters match that are not loaded yet. */
export function hasMore(result: Pick<JournalReportResult, 'totalJournals' | 'journals'>): boolean {
  return result.journals.length < result.totalJournals;
}

/** What the period's totals say about the books, in words as well as in colour. */
export interface JournalVerdict {
  isBalanced: boolean;
  text: string;
}

export function journalVerdict(result: JournalReportResult, money: (value: number) => string): JournalVerdict {
  if (result.totalJournals === 0) {
    return { isBalanced: true, text: 'No journals match these filters.' };
  }

  const count = result.totalJournals === 1 ? '1 journal' : `${result.totalJournals} journals`;

  if (result.isBalanced) {
    return {
      isBalanced: true,
      text: `${count}: debits and credits both come to ${money(result.totalDebit)}.`,
    };
  }

  return {
    isBalanced: false,
    text: `${count}: debits come to ${money(result.totalDebit)} and credits to ${money(result.totalCredit)}, out by ${money(Math.abs(result.totalDebit - result.totalCredit))}. Every posting is checked when it is written, so this should not happen.`,
  };
}
