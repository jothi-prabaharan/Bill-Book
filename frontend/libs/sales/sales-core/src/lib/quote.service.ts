import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SaveQuoteRequest {
  documentDate: string;
  validUntil: string;
  contactId: number;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  billingAddress?: string;
  shippingAddress?: string;
  currencyCode?: string;
  exchangeRate?: number;
  notes?: string;
  termsAndConditions?: string;
  lines: QuoteLineRequest[];
}

export interface QuoteLineRequest {
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

export interface VoidQuoteRequest {
  reason: string;
}

export interface QuoteListItem {
  quoteId: number;
  documentNo: string;
  documentDate: string;
  contactName: string;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  convertedToSalesOrderId?: number;
}

export interface QuoteView {
  quoteId: number;
  documentNo: string;
  documentDate: string;
  validUntil: string;
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
  convertedToSalesOrderId?: number;
  lines: QuoteLineView[];
}

export interface QuoteLineView {
  quoteDetailId: number;
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
  taxes: QuoteLineTaxView[];
}

export interface QuoteLineTaxView {
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
}

@Injectable({
  providedIn: 'root'
})
export class QuoteService {
  private http = inject(HttpClient);
  private apiUrl = '/api/sales/quotes';

  list(): Observable<QuoteListItem[]> {
    return this.http.get<QuoteListItem[]>(this.apiUrl);
  }

  get(quoteId: number): Observable<QuoteView> {
    return this.http.get<QuoteView>(`${this.apiUrl}/${quoteId}`);
  }

  create(request: SaveQuoteRequest): Observable<{ quoteId: number }> {
    return this.http.post<{ quoteId: number }>(this.apiUrl, request);
  }

  update(quoteId: number, request: SaveQuoteRequest): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${quoteId}`, request);
  }

  voidQuote(quoteId: number, request: VoidQuoteRequest): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${quoteId}/void`, request);
  }
}
