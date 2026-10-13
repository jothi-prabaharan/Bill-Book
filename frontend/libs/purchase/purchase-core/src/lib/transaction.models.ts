export interface PurchaseTransactionListItem {
  transactionId: number;
  transactionType: string; // 'Bill' | 'PurchaseOrder' | 'DebitNote' | 'GoodsReceipt'
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  /** The document's currency (TK-187). */
  currencyCode?: string;
  /** The value before tax, in the document's currency (TK-187). */
  taxableAmount?: number;
  totalAmount: number;
  /** Number of line items on the document. */
  itemCount?: number;
  status: string;
  /** A purchase order's Open / PartlyReceived / Closed / Cancelled; null for the other types. */
  fulfilmentStatus?: string | null;
  dueDate?: string | null;
}
