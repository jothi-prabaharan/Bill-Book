import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface AgedReceivableRow {
  customerId: number;
  customerName: string;
  customerCode: string;
  current: number;
  days1To30: number;
  days31To60: number;
  days61To90: number;
  days90Plus: number;
  total: number;
}

export interface CustomerOutstandingInvoiceView {
  invoiceId: number;
  documentNo: string;
  documentDate: string;
  dueDate: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
}

@Injectable({ providedIn: 'root' })
export class OutstandingService {
  private readonly http = inject(HttpClient);

  public async getAgingSummary(): Promise<AgedReceivableRow[]> {
    return firstValueFrom(this.http.get<AgedReceivableRow[]>('/api/sales/outstanding/aging'));
  }

  public async getUnpaidInvoices(customerId: number): Promise<CustomerOutstandingInvoiceView[]> {
    return firstValueFrom(this.http.get<CustomerOutstandingInvoiceView[]>(`/api/sales/outstanding/invoices/${customerId}`));
  }
}
