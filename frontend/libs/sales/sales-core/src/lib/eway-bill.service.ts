import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

// E-way bills (TK-93), for goods leaving on a posted invoice or delivery challan.

export type EwayDocument = 'invoices' | 'delivery-challans';

export type TransportMode = 'Road' | 'Rail' | 'Air' | 'Ship';

export type EwayBillStatus = 'Pending' | 'Generated' | 'Failed' | 'Cancelled' | 'Expired';

export type EwayBillOrigin = 'ByIrn' | 'Standalone' | 'Manual';

export type EwayBillCancelReason = 'Duplicate' | 'OrderCancelled' | 'DataEntryMistake' | 'Other';

export interface EwayBillView {
  ewayBillId: number;
  origin: EwayBillOrigin;
  status: EwayBillStatus;
  ewbNo?: string | null;
  ewbDate?: string | null;
  validUntil?: string | null;
  transportMode: TransportMode;
  vehicleNo?: string | null;
  transporterId?: string | null;
  transporterName?: string | null;
  distanceKm: number;
  message?: string | null;
}

export interface GenerateEwayBillRequest {
  transportMode: TransportMode;
  distanceKm: number;
  vehicleNo?: string | null;
  transporterId?: string | null;
  transporterName?: string | null;
}

export interface UpdatePartBRequest {
  vehicleNo: string;
  transportMode: TransportMode;
  fromPlace: string;
  fromStateCode: string;
  reason: string;
}

export interface CancelEwayBillRequest {
  reason: EwayBillCancelReason;
  remark: string;
}

/** The consignment value above which an e-way bill is needed. Mirrors the server's rule. */
export const EWAY_BILL_THRESHOLD = 50_000;

/** A bill that is in force: generated here or typed in, and not cancelled. */
export function ewayBillLive(view: EwayBillView | null | undefined): boolean {
  return view?.status === 'Generated' || view?.status === 'Pending';
}

/** A bill generated here can be cancelled for 24 hours; a typed one at any time, since the portal never had it. */
export function ewayBillCancellable(view: EwayBillView | null | undefined, now: Date = new Date()): boolean {
  if (!view || view.status !== 'Generated') {
    return false;
  }
  if (view.origin === 'Manual') {
    return true;
  }
  return !!view.ewbDate && now.getTime() - new Date(view.ewbDate).getTime() <= 24 * 60 * 60 * 1000;
}

@Injectable({ providedIn: 'root' })
export class EwayBillService {
  private readonly http = inject(HttpClient);

  private url(document: EwayDocument, id: number): string {
    return `/api/sales/${document}/${id}/eway-bill`;
  }

  /** The document's latest e-way bill, or null when it has none. */
  async get(document: EwayDocument, id: number): Promise<EwayBillView | null> {
    try {
      return await firstValueFrom(this.http.get<EwayBillView>(this.url(document, id)));
    } catch (error: unknown) {
      if ((error as { status?: number })?.status === 404) {
        return null;
      }
      throw error;
    }
  }

  async generate(document: EwayDocument, id: number, request: GenerateEwayBillRequest): Promise<EwayBillView> {
    return firstValueFrom(this.http.post<EwayBillView>(this.url(document, id), request));
  }

  async updatePartB(document: EwayDocument, id: number, request: UpdatePartBRequest): Promise<EwayBillView> {
    return firstValueFrom(this.http.post<EwayBillView>(`${this.url(document, id)}/part-b`, request));
  }

  async cancel(document: EwayDocument, id: number, request: CancelEwayBillRequest): Promise<EwayBillView> {
    return firstValueFrom(this.http.post<EwayBillView>(`${this.url(document, id)}/cancel`, request));
  }
}
