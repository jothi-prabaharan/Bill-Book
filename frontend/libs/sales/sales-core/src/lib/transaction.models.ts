export interface SalesTransactionListItem {
  transactionId: number;
  transactionType: string; // 'Quote' | 'SalesOrder' | 'Invoice' | 'DeliveryChallan' | 'CreditNote'
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  /** The document's currency (TK-187). */
  currencyCode?: string;
  /** The value before tax, in the document's currency (TK-187). */
  taxableAmount?: number;
  totalAmount: number;
  status: string;
  /** A sales order's Open / PartlyDelivered / Closed / Cancelled; null for the other types. */
  fulfilmentStatus?: string | null;
  dueDate?: string | null;
  /** A quote's answer from the customer portal (TK-96). */
  customerResponse?: 'Accepted' | 'Rejected' | null;
}
