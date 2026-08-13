import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SalesOrderService, SalesOrderListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'bb-sales-order-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="page-container">
      <header class="page-header">
        <h1>SalesOrders</h1>
        <button class="primary" routerLink="new">New SalesOrder</button>
      </header>
      
      <table class="data-table">
        <thead>
          <tr>
            <th>Document No</th>
            <th>Date</th>
            <th>Customer</th>
            <th>Amount</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let salesOrder of salesOrders">
            <td><a [routerLink]="[salesOrder.salesOrderId]">{{ salesOrder.documentNo }}</a></td>
            <td>{{ salesOrder.documentDate }}</td>
            <td>{{ salesOrder.contactName }}</td>
            <td>{{ salesOrder.totalAmount }}</td>
            <td>{{ salesOrder.status }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .data-table { width: 100%; border-collapse: collapse; }
    .data-table th, .data-table td { padding: 12px; text-align: left; border-bottom: 1px solid #eee; }
  `]
})
export class SalesOrderListComponent implements OnInit {
  private salesOrderService = inject(SalesOrderService);
  salesOrders: SalesOrderListItem[] = [];

  ngOnInit() {
    this.salesOrderService.list().subscribe(q => this.salesOrders = q);
  }
}
