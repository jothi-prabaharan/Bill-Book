import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

// ---- Request models ----

export interface SaveInvoiceRequest {
  quoteId?: number;
  salesOrderId?: number;
  deliveryChallanId?: number;
  paymentTermId?: number;
  dueDate?: string;
  tillId?: number;
  cashierUserId?: string;
  paymentMode?: string;
  tenderedAmount?: number;
  changeAmount?: number;
  contactId: number;
  documentDate: string;
  currencyCode: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: InvoiceLineRequest[];
}

export interface InvoiceLineRequest {
  itemId?: number;
  description?: string;
  hsnSacCode?: string;
  accountId?: number;
  taxTreatment: string;
  taxMasterId?: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  lineNotes?: string;
}

export interface VoidInvoiceRequest {
  reason: string;
}

// ---- Response / view models ----

export interface InvoiceListItem {
  invoiceId: number;
  salesOrderId?: number;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  status: string;
  dueDate?: string;
  totalAmount: number;
}

export interface InvoiceView {
  invoiceId: number;
  quoteId?: number;
  salesOrderId?: number;
  deliveryChallanId?: number;
  paymentTermId?: number;
  dueDate?: string;
  tillId?: number;
  cashierUserId?: string;
  paymentMode?: string;
  tenderedAmount?: number;
  changeAmount?: number;
  documentNo: string;
  documentDate: string;
  contactId: number;
  contactName: string;
  contactGstin?: string;
  billingAddress?: string;
  shippingAddress?: string;
  placeOfSupplyStateId: number;
  isInterState: boolean;
  currencyCode: string;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmount: number;
  status: string;
  postedAt?: string;
  postedBy?: string;
  voidedAt?: string;
  voidedBy?: string;
  voidReason?: string;
  notes?: string;
  lines: InvoiceLineView[];
}

export interface InvoiceLineView {
  invoiceDetailId: number;
  lineNumber: number;
  itemId?: number;
  itemLabel?: string;
  description?: string;
  hsnSacCode?: string;
  accountId?: number;
  taxTreatment: string;
  taxMasterId?: number;
  quantity: number;
  conversionFactor: number;
  unitPrice: number;
  discountPercent: number;
  discountAmount: number;
  grossAmount: number;
  taxableAmount: number;
  taxAmount: number;
  lineTotal: number;
  lineNotes?: string;
  taxes: InvoiceLineTaxView[];
}

export interface InvoiceLineTaxView {
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
}

// ---- Service ----

@Injectable({
  providedIn: 'root'
})
export class InvoiceService {
  private http = inject(HttpClient);
  private apiUrl = '/api/sales/invoices';

  list(): Observable<InvoiceListItem[]> {
    return this.http.get<InvoiceListItem[]>(this.apiUrl);
  }

  get(invoiceId: number): Observable<InvoiceView> {
    return this.http.get<InvoiceView>(`${this.apiUrl}/${invoiceId}`);
  }

  create(request: SaveInvoiceRequest): Observable<{ invoiceId: number }> {
    return this.http.post<{ invoiceId: number }>(this.apiUrl, request);
  }

  update(invoiceId: number, request: SaveInvoiceRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${invoiceId}`, request);
  }

  post(invoiceId: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${invoiceId}/post`, {});
  }

  voidInvoice(invoiceId: number, request: VoidInvoiceRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${invoiceId}/void`, request);
  }
}
