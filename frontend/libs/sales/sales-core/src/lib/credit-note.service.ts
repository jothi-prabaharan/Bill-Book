import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiDocumentLine } from './document-line-scale';

/**
 * Why the note is raised. Mirrors `CreditNoteReason` on the server, value for
 * value — the screen used to offer seven reasons numbered 1 to 7 against a
 * server enum of five numbered from 0, so "Sales Return" was saved as a price
 * correction and no stock ever came back.
 */
export enum CreditNoteReason {
  SalesReturn = 0,
  PriceCorrection = 1,
  PostSaleDiscount = 2,
  Deficiency = 3,
  Cancellation = 4,
}

export interface CreditNoteListItem {
  creditNoteId: number;
  invoiceId: number;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  /** Draft / ReadyToPost / Posted / Void, by name. */
  status: string;
  totalAmount: number;
}

export interface CreditNoteView {
  creditNoteId: number;
  invoiceId: number;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName: string;
  contactGstin?: string | null;
  status: string;
  reasonCode: CreditNoteReason;
  currencyCode: string;
  exchangeRate: number;
  notes?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  voidReason?: string | null;
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

export interface CreditNoteLineView extends ApiDocumentLine {
  creditNoteDetailId: number;
  invoiceDetailId: number;
  itemLabel?: string | null;
  lineTotal: number;
  taxAmount: number;
}

export interface SaveCreditNoteRequest {
  invoiceId: number;
  documentDate: string;
  contactId: number;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  reasonCode: CreditNoteReason;
  currencyCode?: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: SaveCreditNoteLineRequest[];
}

/**
 * One line to save. Every line names the invoice line it corrects, and carries
 * that line's item — the server refuses anything else.
 */
export interface SaveCreditNoteLineRequest {
  invoiceDetailId: number;
  itemId?: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxGroupId?: number;
}

export interface VoidCreditNoteRequest {
  reason: string;
}

/** What the server answers a save, post or void with. */
export interface CreditNoteResult {
  creditNoteId: number;
}

/**
 * Sales › Credit notes. Promises rather than streams, like the invoice and
 * challan services, so a refusal can be caught and shown in its own words.
 */
@Injectable({
  providedIn: 'root'
})
export class CreditNoteService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/sales/credit-notes';

  async list(from?: string, to?: string): Promise<CreditNoteListItem[]> {
    const params: Record<string, string> = {};
    if (from) {
      params['from'] = from;
    }
    if (to) {
      params['to'] = to;
    }

    return firstValueFrom(this.http.get<CreditNoteListItem[]>(this.baseUrl, { params }));
  }

  async get(id: number): Promise<CreditNoteView> {
    return firstValueFrom(this.http.get<CreditNoteView>(`${this.baseUrl}/${id}`));
  }

  async create(request: SaveCreditNoteRequest): Promise<CreditNoteResult> {
    return firstValueFrom(this.http.post<CreditNoteResult>(this.baseUrl, request));
  }

  async update(id: number, request: SaveCreditNoteRequest): Promise<CreditNoteResult> {
    return firstValueFrom(this.http.put<CreditNoteResult>(`${this.baseUrl}/${id}`, request));
  }

  /** Claims the note against its invoice, returns any goods, and reverses the revenue and tax. */
  async post(id: number): Promise<CreditNoteResult> {
    return firstValueFrom(this.http.post<CreditNoteResult>(`${this.baseUrl}/${id}/post`, {}));
  }

  /** Withdraws a note, with a reason. A posted sales return cannot be voided. */
  async voidCreditNote(id: number, request: VoidCreditNoteRequest): Promise<CreditNoteResult> {
    return firstValueFrom(this.http.post<CreditNoteResult>(`${this.baseUrl}/${id}/void`, request));
  }
}
