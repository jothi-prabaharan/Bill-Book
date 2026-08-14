import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SaveSalesOrderRequest {
  documentDate: string;
  deliveryDate: string;
  contactId: number;
  quoteId?: number;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  billingAddress?: string;
  shippingAddress?: string;
  currencyCode?: string;
  exchangeRate?: number;
  notes?: string;
  termsAndConditions?: string;
  lines: SalesOrderLineRequest[];
}

export interface SalesOrderLineRequest {
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

export interface VoidSalesOrderRequest {
  reason: string;
}

export interface SalesOrderListItem {
  salesOrderId: number;
  documentNo: string;
  documentDate: string;
  contactName: string;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  invoicedDocumentId?: number;
}

export interface SalesOrderView {
  salesOrderId: number;
  documentNo: string;
  documentDate: string;
  deliveryDate: string;
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
  termsAndConditions?: string;
  invoicedDocumentId?: number;
  lines: SalesOrderLineView[];
}

export interface SalesOrderLineView {
  salesOrderDetailId: number;
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
  taxes: SalesOrderLineTaxView[];
}

export interface SalesOrderLineTaxView {
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
}

@Injectable({
  providedIn: 'root'
})
export class SalesOrderService {
  private http = inject(HttpClient);
  private apiUrl = '/api/sales/sales-orders';

  list(): Observable<SalesOrderListItem[]> {
    return this.http.get<SalesOrderListItem[]>(this.apiUrl);
  }

  get(salesOrderId: number): Observable<SalesOrderView> {
    return this.http.get<SalesOrderView>(`${this.apiUrl}/${salesOrderId}`);
  }

  create(request: SaveSalesOrderRequest): Observable<{ salesOrderId: number }> {
    return this.http.post<{ salesOrderId: number }>(this.apiUrl, request);
  }

  update(salesOrderId: number, request: SaveSalesOrderRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${salesOrderId}`, request);
  }

  approve(salesOrderId: number): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${salesOrderId}/approve`, {});
  }

  voidOrder(salesOrderId: number, request: VoidSalesOrderRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${salesOrderId}/void`, request);
  }
}
