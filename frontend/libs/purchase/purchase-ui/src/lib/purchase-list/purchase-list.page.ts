import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TransactionService, PurchaseTransactionListItem } from '@bill-book/purchase-core';

@Component({
  selector: 'bb-purchase-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './purchase-list.page.html'
})
export class PurchaseListPage implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);

  transactions: PurchaseTransactionListItem[] = [];
  selectedType: string = ''; // Empty string means 'All'

  ngOnInit() {
    this.loadTransactions();
  }

  loadTransactions() {
    this.transactionService.list(this.selectedType).subscribe(t => this.transactions = t);
  }

  onTypeChange() {
    this.loadTransactions();
  }

  getRouteForTransaction(transaction: PurchaseTransactionListItem): string {
    switch (transaction.transactionType) {
      case 'Bill': return `/purchase/bills/${transaction.transactionId}`;
      case 'PurchaseOrder': return `/purchase/purchase-orders/${transaction.transactionId}`;
      case 'GoodsReceipt': return `/purchase/goods-receipts/${transaction.transactionId}`;
      case 'DebitNote': return `/purchase/debit-notes/${transaction.transactionId}`;
      default: return `/purchase`;
    }
  }

  navigateToTransaction(transaction: PurchaseTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }
}
