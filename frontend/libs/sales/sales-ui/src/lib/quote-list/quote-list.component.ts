import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QuoteService, QuoteListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'bb-quote-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="page-container">
      <header class="page-header">
        <h1>Quotes</h1>
        <button class="primary" routerLink="new">New Quote</button>
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
          <tr *ngFor="let quote of quotes">
            <td><a [routerLink]="[quote.quoteId]">{{ quote.documentNo }}</a></td>
            <td>{{ quote.documentDate }}</td>
            <td>{{ quote.contactName }}</td>
            <td>{{ quote.totalAmount }}</td>
            <td>{{ quote.status }}</td>
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
export class QuoteListComponent implements OnInit {
  private quoteService = inject(QuoteService);
  quotes: QuoteListItem[] = [];

  ngOnInit() {
    this.quoteService.list().subscribe(q => this.quotes = q);
  }
}
