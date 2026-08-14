import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { InvoiceService, InvoiceListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'bb-invoice-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss'
})
export class InvoiceListComponent implements OnInit {
  private invoiceService = inject(InvoiceService);
  invoices: InvoiceListItem[] = [];

  ngOnInit() {
    this.invoiceService.list().subscribe(list => this.invoices = list);
  }
}
