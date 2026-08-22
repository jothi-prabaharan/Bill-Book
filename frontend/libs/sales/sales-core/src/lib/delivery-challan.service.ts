import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

/** A challan on the list screen. Mirrors `DeliveryChallanListItem` on the server. */
export interface DeliveryChallanListItem {
  deliveryChallanId: number;
  salesOrderId: number | null;
  documentDate: string;
  documentNo: string;
  contactId: number;
  /** Read from the contact master on the way out; never stored on the document. */
  contactName: string;
  /** Draft / ReadyToPost / Posted / Void, as the shared lifecycle names them. */
  status: string;
  dispatchDate: string;
  totalAmount: number;
}

export interface SaveDeliveryChallanRequest {
  deliveryChallanId?: number;
  salesOrderId?: number;
  documentDate: string;
  contactId: number;
  challanType: number;
  vehicleNo?: string;
  transporterName?: string;
  ewayBillNo?: string;
  ewayBillDate?: string;
  dispatchDate: string;
  currencyCode?: string;
  exchangeRate?: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: SaveDeliveryChallanLineRequest[];
}

export interface SaveDeliveryChallanLineRequest {
  itemId: number;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  taxGroupIds?: number[];
  hsnSacCode?: string;
  description?: string;
  accountId?: number;
  taxTreatment?: string;
  taxMasterId?: number;
}

export interface DeliveryChallanView {
  deliveryChallanId: number;
  salesOrderId?: number;
  documentDate: string;
  documentNo: string;
  contactId: number;
  status: string;
  challanType: number;
  vehicleNo?: string;
  transporterName?: string;
  ewayBillNo?: string;
  ewayBillDate?: string;
  dispatchDate: string;
  currencyCode: string;
  exchangeRate: number;
  notes?: string;
  billingAddress?: string;
  shippingAddress?: string;
  lines: any[];
}

@Injectable({ providedIn: 'root' })
export class DeliveryChallanService {
  private http = inject(HttpClient);
  private url = '/api/sales/delivery-challans';

  /**
   * The challans in a date range, newest first. Both bounds are optional — the
   * list opens on everything.
   */
  list(from?: string, to?: string): Observable<DeliveryChallanListItem[]> {
    const params: string[] = [];
    if (from) {
      params.push(`from=${from}`);
    }
    if (to) {
      params.push(`to=${to}`);
    }

    const query = params.length > 0 ? `?${params.join('&')}` : '';
    return this.http.get<DeliveryChallanListItem[]>(`${this.url}${query}`);
  }

  get(id: number): Observable<DeliveryChallanView> {
    return this.http.get<DeliveryChallanView>(`${this.url}/${id}`);
  }

  /**
   * Dispatches: issues the stock, releases the order's reservation, and posts
   * to the ledger only if the challan is a sale.
   */
  post(id: number): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/post`, {});
  }

  create(request: SaveDeliveryChallanRequest): Observable<{ deliveryChallanId: number }> {
    return this.http.post<{ deliveryChallanId: number }>(this.url, request);
  }

  update(id: number, request: SaveDeliveryChallanRequest): Observable<void> {
    return this.http.put<void>(`${this.url}/${id}`, request);
  }

  voidChallan(id: number, request: { reason: string }): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/void`, request);
  }
}
