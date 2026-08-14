import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

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

  get(id: number): Observable<DeliveryChallanView> {
    return this.http.get<DeliveryChallanView>(`${this.url}/${id}`);
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
