import { ChangeDetectionStrategy } from '@angular/core';
import { Component, inject, OnInit } from '@angular/core';
import { DataGridComponent, ColumnDef } from '@bill-book/ui-components';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TransactionService, PurchaseTransactionListItem } from '@bill-book/purchase-core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-purchase-list',
  standalone: true,
  imports: [DataGridComponent, CommonModule, RouterModule, FormsModule],
  templateUrl: './purchase-list.page.html'
})
export class PurchaseListPage implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);

  transactions: PurchaseTransactionListItem[] = [];
  selectedType: string = '';
  openFilter: string | null = null;
  filterOp: string = 'contains';
  filterVal: string = '';

  toggleFilter(col: string, event: Event) {
    event.stopPropagation();
    this.openFilter = this.openFilter === col ? null : col;
  }

  // Mock properties for parity with design


  ngOnInit() {
    this.loadTransactions();
  }

  loadTransactions() {
    this.transactionService.list(this.selectedType).subscribe(t => this.transactions = t);
  }

  setType(type: string) {
    this.selectedType = type;
    this.loadTransactions();
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

  columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date' },
    { field: 'transactionType', header: 'Type' },
    { field: 'documentNo', header: 'Number' },
    { field: 'contactName', header: 'Vendor' },
    { field: 'totalAmount', header: 'Amount' },
    { field: 'status', header: 'Status' }
  ];

  navigateToTransaction(transaction: PurchaseTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }
}



