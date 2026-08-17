import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TransactionService, SalesTransactionListItem } from '@bill-book/sales-core';

@Component({
  selector: 'bb-sales-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './sales-list.component.html'
})
export class SalesListComponent implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);

  transactions: SalesTransactionListItem[] = [];
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

  getRouteForTransaction(transaction: SalesTransactionListItem): string {
    switch (transaction.transactionType) {
      case 'Quote': return `/sales/quotes/${transaction.transactionId}`;
      case 'SalesOrder': return `/sales/sales-orders/${transaction.transactionId}`;
      case 'Invoice': return `/sales/invoices/${transaction.transactionId}`;
      case 'CreditNote': return `/sales/credit-notes/${transaction.transactionId}`;
      default: return `/sales`;
    }
  }

  navigateToTransaction(transaction: SalesTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }
}
