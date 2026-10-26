/**
 * The Quotation Track Report's contract (TK-231), field for field with
 * `QuotationTrackReportModels.cs`, and the pure steps the page takes from an answer: each quote dated
 * in a period with its outcome (open, expired, accepted, converted to an order or an invoice, or
 * declined), the order or invoice it became, and the conversion rate by count and by value. Values are
 * in the branch's base currency; a quote in another currency also carries its own total. Outcomes are as
 * they stand today. Dates are ISO `yyyy-MM-dd`.
 */

/** Where a quote stands today; a conversion wins over what the customer answered, and a void one is set apart. */
export type QuoteOutcome = 'Open' | 'Expired' | 'Accepted' | 'ConvertedToOrder' | 'ConvertedToInvoice' | 'Declined' | 'Void';

/** What the report is asked for. Both dates are required; the rest narrow it. */
export interface QuotationTrackRequest {
  fromDate: string;
  toDate: string;
  /** Only this customer; every customer when blank. */
  contactId?: number | null;
  /** Only quotes with this outcome; every outcome but void when blank. */
  outcome?: QuoteOutcome | null;
  pageNumber?: number;
  pageSize?: number;
}

/** The order or invoice a quote was turned into. */
export interface QuoteConversion {
  /** SOR for a sales order, INV (or POS) for an invoice. */
  typeCode: string;
  documentId: number;
  documentNo: string;
  documentDate: string;
}

/** One quote with its outcome and what it became. */
export interface QuotationTrackQuote {
  quoteId: number;
  documentNo: string;
  documentDate: string;
  /** The last day the customer can take it up. */
  validUntil: string;
  contactId: number;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The total in the quote's own currency. */
  totalInCurrency: number;
  total: number;
  outcome: QuoteOutcome;
  /** The invoice, else the earliest order, raised from it; null when it was not converted. */
  convertedTo: QuoteConversion | null;
  /** Days from the quote to that document; null when it was not converted. */
  daysToConvert: number | null;
}

/** The quotes raised and how many became orders or invoices; void ones are counted in neither. */
export interface QuotationTrackSummary {
  raised: number;
  raisedValue: number;
  converted: number;
  convertedValue: number;
  /** Converted over raised, as a percentage; null when none were raised. */
  conversionRateByCount: number | null;
  /** Converted value over raised value, as a percentage; null when none were raised. */
  conversionRateByValue: number | null;
}

export interface QuotationTrackResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  /** This page of quotes, newest first. */
  quotes: QuotationTrackQuote[];
  /** How many quotes the filters match, all pages. */
  totalQuotes: number;
  pageNumber: number;
  pageSize: number;
  /** How many quotes have each outcome, none left out; void is counted but is not part of All. */
  outcomeCounts: Record<QuoteOutcome, number>;
  /** The quotes raised and the conversion rate, whatever outcome is chosen. */
  summary: QuotationTrackSummary;
  /** The value of every quote the filters match, in base currency. */
  totalValue: number;
}

/** The outcomes in the order the tabs show them: a quote's life, from waiting to settled. */
export const QUOTE_OUTCOMES: readonly { value: QuoteOutcome; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Accepted', label: 'Accepted' },
  { value: 'ConvertedToOrder', label: 'Converted to order' },
  { value: 'ConvertedToInvoice', label: 'Converted to invoice' },
  { value: 'Declined', label: 'Declined' },
  { value: 'Void', label: 'Void' },
];

/** How an outcome reads on the page. */
export function quoteOutcomeLabel(outcome: QuoteOutcome): string {
  return QUOTE_OUTCOMES.find((o) => o.value === outcome)?.label ?? outcome;
}

/** The colour of an outcome's tag: converted reads as won, declined as lost, expired as a warning. */
export function quoteOutcomeTag(outcome: QuoteOutcome): string {
  switch (outcome) {
    case 'ConvertedToOrder':
    case 'ConvertedToInvoice':
      return 'tag-success';
    case 'Accepted':
      return 'tag-info';
    case 'Expired':
      return 'tag-warning';
    case 'Declined':
      return 'tag-danger';
    case 'Open':
      return 'tag-outline';
    default:
      return 'tag-neutral';
  }
}

/** A tab above the grid: an outcome ('' is All), its label and how many quotes have it. */
export interface QuotationTrackTab {
  value: string;
  label: string;
  count: number;
}

/**
 * The tabs: All, then every outcome, a nought included so a tab can say nothing is in it. The counts
 * are the server's, because a page cannot count the pages it was not sent. All is every outcome but
 * void, the list a person gets without choosing one.
 */
export function quotationTrackTabs(counts: Readonly<Partial<Record<QuoteOutcome, number>>> | undefined): QuotationTrackTab[] {
  const count = (outcome: QuoteOutcome): number => counts?.[outcome] ?? 0;
  const all = QUOTE_OUTCOMES.filter((o) => o.value !== 'Void').reduce((sum, o) => sum + count(o.value), 0);

  return [{ value: '', label: 'All', count: all }, ...QUOTE_OUTCOMES.map((o) => ({ ...o, count: count(o.value) }))];
}

/** Whether a quote has been turned into an order or an invoice. */
export function isConverted(outcome: QuoteOutcome): boolean {
  return outcome === 'ConvertedToOrder' || outcome === 'ConvertedToInvoice';
}

/** Whether a quote is in a currency other than the branch's, so its own total is worth showing. */
export function isForeignQuote(quote: Pick<QuotationTrackQuote, 'currencyCode'>, baseCurrency: string): boolean {
  return quote.currencyCode !== '' && baseCurrency !== '' && quote.currencyCode.toUpperCase() !== baseCurrency.toUpperCase();
}

/** Where a quote opens. */
export function quoteLink(quoteId: number): string {
  return `/sales/quotes/${quoteId}`;
}

/** Where the document a quote became opens: an invoice (a till sale included) or a sales order; null for anything else. */
export function conversionLink(conversion: Pick<QuoteConversion, 'typeCode' | 'documentId'>): string | null {
  switch (conversion.typeCode) {
    case 'INV':
    case 'POS':
      return `/sales/invoices/${conversion.documentId}`;
    case 'SOR':
      return `/sales/sales-orders/${conversion.documentId}`;
    default:
      return null;
  }
}

/** How long a conversion took, in words: the same day, a day, or n days. */
export function daysToConvertLabel(days: number | null): string {
  if (days === null) {
    return '';
  }

  return days === 0 ? 'Same day' : days === 1 ? '1 day' : `${days} days`;
}

/** A conversion rate to one decimal place with its percent sign, or a dash when nothing was raised. */
export function conversionRateLabel(rate: number | null): string {
  return rate === null ? '—' : `${rate.toFixed(1)}%`;
}

export type QuotationTrackRowKind = 'quote' | 'total';

/** One row of the grid: a quote, or the value of every quote the filters match. */
export interface QuotationTrackGridRow {
  kind: QuotationTrackRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  /** The quote's number, or the label on the totals row. */
  name: string;
  /** Where the number opens; null on the totals row. */
  link: string | null;
  documentDate: string | null;
  validUntil: string | null;
  contactCode: string;
  contactName: string;
  currencyCode: string;
  /** The quote's own total when it is in another currency, else null. */
  ownTotal: number | null;
  total: number;
  outcome: QuoteOutcome | null;
  /** The number of the order or invoice it became, and where it opens; null when it was not converted. */
  convertedNo: string | null;
  convertedLink: string | null;
  /** The type of that document, for the page to word it: an order or an invoice. */
  convertedKind: 'order' | 'invoice' | null;
  daysToConvert: number | null;
}

/**
 * The grid's rows: each quote as the server sent it, then the value of every quote the filters match,
 * which is the server's and not the loaded rows' sum, so it stays right until the last page is loaded.
 * Nothing is worked out here beyond the layout.
 */
export function quotationTrackRows(result: QuotationTrackResult): QuotationTrackGridRow[] {
  const rows: QuotationTrackGridRow[] = result.quotes.map((q) => ({
    kind: 'quote',
    key: `q:${q.quoteId}`,
    name: q.documentNo,
    link: quoteLink(q.quoteId),
    documentDate: q.documentDate,
    validUntil: q.validUntil,
    contactCode: q.contactCode,
    contactName: q.contactName,
    currencyCode: q.currencyCode,
    ownTotal: isForeignQuote(q, result.baseCurrencyCode) ? q.totalInCurrency : null,
    total: q.total,
    outcome: q.outcome,
    convertedNo: q.convertedTo?.documentNo ?? null,
    convertedLink: q.convertedTo ? conversionLink(q.convertedTo) : null,
    convertedKind: q.convertedTo ? (q.convertedTo.typeCode === 'SOR' ? 'order' : 'invoice') : null,
    daysToConvert: q.daysToConvert,
  }));

  if (result.quotes.length > 0) {
    rows.push({
      kind: 'total',
      key: 't',
      name: result.quotes.length < result.totalQuotes ? `Total of all ${result.totalQuotes} quotes` : 'Total',
      link: null,
      documentDate: null,
      validUntil: null,
      contactCode: '',
      contactName: '',
      currencyCode: '',
      ownTotal: null,
      total: result.totalValue,
      outcome: null,
      convertedNo: null,
      convertedLink: null,
      convertedKind: null,
      daysToConvert: null,
    });
  }

  return rows;
}

/** The pages loaded so far with the next one added; the counts, summary and total are the latest answer's. */
export function appendQuotationTrackPage(loaded: QuotationTrackResult, next: QuotationTrackResult): QuotationTrackResult {
  return { ...next, quotes: [...loaded.quotes, ...next.quotes] };
}

/** Whether there are quotes the filters match that are not loaded yet. */
export function hasMoreQuotationTrack(result: Pick<QuotationTrackResult, 'totalQuotes' | 'quotes'>): boolean {
  return result.quotes.length < result.totalQuotes;
}
