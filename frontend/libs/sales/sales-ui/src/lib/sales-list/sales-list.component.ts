import { ChangeDetectionStrategy } from '@angular/core';
import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TransactionService, SalesTransactionListItem } from '@bill-book/sales-core';
import { DataGridComponent, DataGridCellTemplateDirective, ColumnDef } from '@bill-book/ui-components';

changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-list',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, DataGridComponent, DataGridCellTemplateDirective],
  templateUrl: './sales-list.component.html'
})
export class SalesListComponent implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);

  transactions: SalesTransactionListItem[] = [];
  selectedType: string = '';

  columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date' },
    { field: 'transactionType', header: 'Type' },
    { field: 'documentNo', header: 'Number' },
    { field: 'contactName', header: 'Customer' },
    { field: 'totalAmount', header: 'Amount', align: 'right' },
    { field: 'status', header: 'Status' }
  ];

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

  getRouteForTransaction(transaction: SalesTransactionListItem): string {
    switch (transaction.transactionType) {
      case 'Quote': return `/sales/quotes/${transaction.transactionId}`;
      case 'SalesOrder': return `/sales/sales-orders/${transaction.transactionId}`;
      case 'Invoice': return `/sales/invoices/${transaction.transactionId}`;
      case 'DeliveryChallan': return `/sales/delivery-challans/${transaction.transactionId}`;
      case 'CreditNote': return `/sales/credit-notes/${transaction.transactionId}`;
      default: return `/sales`;
    }
  }

  navigateToTransaction(transaction: SalesTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }
}

