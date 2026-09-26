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
