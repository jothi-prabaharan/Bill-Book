export interface PurchaseTransactionListItem {
  transactionId: number;
  transactionType: string; // 'Bill' | 'PurchaseOrder' | 'DebitNote' | 'GoodsReceipt'
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  totalAmount: number;
  status: string;
  dueDate?: string | null;
}
