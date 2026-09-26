export interface SalesTransactionListItem {
  transactionId: number;
  transactionType: string; // 'Quote' | 'SalesOrder' | 'Invoice' | 'CreditNote'
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  totalAmount: number;
  status: string;
  dueDate?: string | null;
  /** A quote's answer from the customer portal (TK-96). */
  customerResponse?: 'Accepted' | 'Rejected' | null;
}
