/** The portal dashboard's figures (TK-95), from Reporting's `api/portal/summary`. */
export interface PortalSummary {
  outstanding: number;
  overdue: number;
  tradeValueThisYear: number;
  tradeValueAllTime: number;
  financialYearStart: string;
  recent: PortalRecentDocument[];
}

export interface PortalRecentDocument {
  kind: 'Invoice' | 'CreditNote';
  id: number;
  documentNo: string;
  documentDate: string;
  totalAmount: number;
}

export type PortalInvoiceStatus = 'Open' | 'PartPaid' | 'Paid' | 'Overdue' | 'Void';

export interface PortalInvoiceItem {
  invoiceId: number;
  documentNo: string;
  documentDate: string;
  dueDate: string | null;
  currencyCode: string;
  totalAmount: number;
  outstandingAmount: number | null;
  status: PortalInvoiceStatus;
}

export interface PortalInvoiceLine {
  lineNumber: number;
  description: string;
  hsnSacCode: string | null;
  quantity: number;
  unitPrice: number;
  taxAmount: number;
  lineTotal: number;
}

export interface PortalInvoiceDetail {
  invoice: PortalInvoiceItem;
  subTotal: number;
  discountAmount: number;
  taxAmount: number;
  roundOffAmount: number;
  lines: PortalInvoiceLine[];
}

export interface PortalStatementLine {
  ledgerDate: string;
  documentNo: string;
  transactionTypeCode: string;
  transactionId: number;
  description: string | null;
  debit: number;
  credit: number;
  balance: number;
}

export interface PortalStatementSide {
  openingBalance: number;
  transactions: PortalStatementLine[];
  closingBalance: number;
}

export interface PortalStatement {
  receivable: PortalStatementSide;
  payable: PortalStatementSide | null;
}

/** How an invoice's status reads to a customer. */
export function statusLabel(status: PortalInvoiceStatus): string {
  switch (status) {
    case 'PartPaid':
      return 'Part-paid';
    default:
      return status;
  }
}

/** Which colour an invoice's status wears: a warning for money late, muted for void. */
export function statusTone(status: PortalInvoiceStatus): 'danger' | 'success' | 'muted' | 'neutral' {
  switch (status) {
    case 'Overdue':
      return 'danger';
    case 'Paid':
      return 'success';
    case 'Void':
      return 'muted';
    default:
      return 'neutral';
  }
}

/** A statement line opens its invoice in the portal when it is one: an invoice or a till sale. */
export function invoiceLink(line: PortalStatementLine): number | null {
  return line.transactionTypeCode === 'INV' || line.transactionTypeCode === 'POS' ? line.transactionId : null;
}

/** A quote as the portal lists it (TK-96). */
export interface PortalQuoteItem {
  quoteId: number;
  documentNo: string;
  documentDate: string;
  validUntil: string;
  currencyCode: string;
  totalAmount: number;
  customerResponse: 'None' | 'Accepted' | 'Rejected';
  respondedAt: string | null;
  respondedByName: string | null;
  canRespond: boolean;
}

/** How a quote's state reads to the customer. */
export function quoteState(quote: PortalQuoteItem, today: string): string {
  if (quote.customerResponse === 'Accepted') return 'You accepted this quote';
  if (quote.customerResponse === 'Rejected') return 'You declined this quote';
  if (quote.validUntil < today) return 'No longer valid';
  return quote.canRespond ? 'Waiting for your answer' : 'Already turned into an order';
}

/** A support ticket as the portal lists it (TK-97). */
export interface PortalTicketItem {
  ticketId: number;
  subject: string;
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
  raisedAt: string | null;
  lastMessageAt: string | null;
}

export interface PortalTicketMessage {
  messageId: number;
  from: 'You' | 'Support';
  body: string;
  sentAt: string | null;
}

export interface PortalTicketDetail {
  ticket: PortalTicketItem;
  description: string | null;
  messages: PortalTicketMessage[];
}

/** How a ticket's status reads to the customer. */
export function ticketStatusLabel(status: PortalTicketItem['status']): string {
  switch (status) {
    case 'InProgress':
      return 'In progress';
    default:
      return status;
  }
}

/** An online payment as the portal shows its result (TK-98): from the server, never from the redirect. */
export interface PortalPayment {
  onlinePaymentId: number;
  amount: number;
  currencyCode: string;
  status: 'Created' | 'Paid' | 'Failed' | 'Refunded';
  receiptNo: string | null;
  note: string | null;
}

/** What the payer has chosen on the pay screen. */
export interface PaymentChoice {
  invoiceId: number;
  outstanding: number;
  selected: boolean;
  amount: number;
}

/**
 * The payment the screen will ask for: each selected invoice's amount, and any
 * extra on account. Null when nothing valid is chosen — an amount above what an
 * invoice owes, or nothing above zero at all.
 */
export function paymentTotal(choices: PaymentChoice[], extra: number): number | null {
  const chosen = choices.filter(c => c.selected);
  if (chosen.some(c => !(c.amount > 0) || c.amount > c.outstanding + 0.005)) return null;
  if (extra < 0) return null;
  const total = Math.round((chosen.reduce((sum, c) => sum + c.amount, 0) + extra) * 100) / 100;
  return total > 0 ? total : null;
}
