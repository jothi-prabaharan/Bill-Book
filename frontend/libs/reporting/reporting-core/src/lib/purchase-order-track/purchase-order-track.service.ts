import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { PurchaseOrderTrackRequest, PurchaseOrderTrackResult } from './purchase-order-track.models';

/**
 * The Purchase Order Track Report's own endpoint (TK-240), and the list its vendor filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseOrderTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's orders with their progress and lines, the counts for the tabs and the totals of every match. */
  async run(request: PurchaseOrderTrackRequest): Promise<PurchaseOrderTrackResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.progress) {
      params = params.set('progress', request.progress);
    }
    if (request.outstandingOnly) {
      params = params.set('outstandingOnly', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<PurchaseOrderTrackResult>('/api/statements/purchase-order-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/purchase-order-track-report/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
