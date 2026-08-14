import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CreditNoteListItem {
  creditNoteId: number;
  invoiceId: number;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  status: number;
  totalAmount: number;
}

export interface CreditNoteView {
  creditNoteId: number;
  invoiceId: number;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  status: number;
  reasonCode: number;
  currencyCode: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  placeOfSupplyStateId: number;
  isInterState: boolean;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmount: number;
  totalAmountBase: number;
  lines: CreditNoteLineView[];
}

export interface CreditNoteLineView {
  creditNoteDetailId: number;
  invoiceDetailId: number;
  itemId?: number;
  itemLabel?: string;
  description?: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  discountAmount: number;
  lineTotal: number;
  taxAmount: number;
  taxes: CreditNoteLineTaxView[];
}

export interface CreditNoteLineTaxView {
  taxComponent: number;
  subAccountId: number;
  amount: number;
}

export interface SaveCreditNoteRequest {
  creditNoteId?: number;
  invoiceId: number;
  documentDate: string;
  contactId: number;
  reasonCode: number;
  currencyCode?: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: SaveCreditNoteLineRequest[];
}

export interface SaveCreditNoteLineRequest {
  invoiceDetailId: number;
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxGroupIds: number[];
}

@Injectable({
  providedIn: 'root'
})
export class CreditNoteService {
  private http = inject(HttpClient);
  private baseUrl = '/api/sales/CreditNotes';

  list(from?: string, to?: string): Observable<CreditNoteListItem[]> {
    let url = this.baseUrl;
    const params = new URLSearchParams();
    if (from) params.append('from', from);
    if (to) params.append('to', to);
    if (params.toString()) url += `?${params.toString()}`;
    
    return this.http.get<CreditNoteListItem[]>(url);
  }

  get(id: number): Observable<CreditNoteView> {
    return this.http.get<CreditNoteView>(`${this.baseUrl}/${id}`);
  }

  save(request: SaveCreditNoteRequest): Observable<{ creditNoteId: number }> {
    return this.http.post<{ creditNoteId: number }>(this.baseUrl, request);
  }

  update(id: number, request: SaveCreditNoteRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  post(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/post`, {});
  }

  void(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/void`, {});
  }
}
