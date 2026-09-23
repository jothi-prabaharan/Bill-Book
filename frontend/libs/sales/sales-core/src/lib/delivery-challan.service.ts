import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiDocumentLine } from './document-line-scale';

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

/**
 * What the goods are going out for. Mirrors `ChallanType` on the server, value
 * for value — a label list that disagreed with it once sent "Transfer" as
 * approval.
 */
export enum ChallanType {
  Sale = 0,
  JobWork = 1,
  Approval = 2,
  BranchTransfer = 3,
  Sample = 4,
}

export interface SaveDeliveryChallanRequest {
  salesOrderId?: number;
  documentDate: string;
  contactId: number;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  challanType: ChallanType;
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

/**
 * One line to save. The server takes the item, quantity, price, discount and
 * tax group, and — on a challan against an order — which order line it
 * delivers. Every line must name one then, and none may otherwise.
 */
export interface SaveDeliveryChallanLineRequest extends ApiDocumentLine {
  salesOrderDetailId?: number;
}

export interface DeliveryChallanLineView extends ApiDocumentLine {
  deliveryChallanDetailId: number;
  salesOrderDetailId?: number | null;
  itemLabel?: string | null;
}

export interface DeliveryChallanView {
  deliveryChallanId: number;
  salesOrderId?: number | null;
  documentDate: string;
  documentNo: string;
  contactId: number;
  contactName?: string;
  contactGstin?: string | null;
  status: string;
  challanType: ChallanType;
  vehicleNo?: string | null;
  transporterName?: string | null;
  ewayBillNo?: string | null;
  ewayBillDate?: string | null;
  dispatchDate: string;
  currencyCode: string;
  exchangeRate: number;
  notes?: string | null;
  billingAddress?: string | null;
  shippingAddress?: string | null;
  voidReason?: string | null;
  lines: DeliveryChallanLineView[];
}

export interface VoidDeliveryChallanRequest {
  reason: string;
}

/** What the server answers a save, post or void with. */
export interface DeliveryChallanResult {
  deliveryChallanId: number;
}

/**
 * Sales › Delivery challans.
 *
 * Promises rather than streams, like the invoice and order services: every call
 * is one request and one answer, and awaiting it is what lets a refusal be caught
 * and shown with the server's own words.
 */
@Injectable({ providedIn: 'root' })
export class DeliveryChallanService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/sales/delivery-challans';

  /**
   * The challans in a date range, newest first. Both bounds are optional — the
   * list opens on everything.
   */
  async list(from?: string, to?: string): Promise<DeliveryChallanListItem[]> {
    const params: Record<string, string> = {};
    if (from) {
      params['from'] = from;
    }
    if (to) {
      params['to'] = to;
    }

    return firstValueFrom(this.http.get<DeliveryChallanListItem[]>(this.url, { params }));
  }

  async get(id: number): Promise<DeliveryChallanView> {
    return firstValueFrom(this.http.get<DeliveryChallanView>(`${this.url}/${id}`));
  }

  async create(request: SaveDeliveryChallanRequest): Promise<DeliveryChallanResult> {
    return firstValueFrom(this.http.post<DeliveryChallanResult>(this.url, request));
  }

  async update(id: number, request: SaveDeliveryChallanRequest): Promise<DeliveryChallanResult> {
    return firstValueFrom(this.http.put<DeliveryChallanResult>(`${this.url}/${id}`, request));
  }

  /** Dispatches: issues the stock and moves the order's delivered and reserved quantities. */
  async post(id: number): Promise<DeliveryChallanResult> {
    return firstValueFrom(this.http.post<DeliveryChallanResult>(`${this.url}/${id}/post`, {}));
  }

  /** Withdraws a draft. A dispatched challan cannot be voided — raise a return. */
  async voidChallan(id: number, request: VoidDeliveryChallanRequest): Promise<DeliveryChallanResult> {
    return firstValueFrom(
      this.http.post<DeliveryChallanResult>(`${this.url}/${id}/void`, request),
    );
  }
}
