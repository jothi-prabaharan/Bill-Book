import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { ReceiveOrderTrackRequest, ReceiveOrderTrackResult } from './receive-order-track.models';

/**
 * The Receive Order Track Report's own endpoint (TK-242), and the list its vendor filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ReceiveOrderTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's goods receipts with their billing status, the counts for the tabs and the totals of every match. */
  async run(request: ReceiveOrderTrackRequest): Promise<ReceiveOrderTrackResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.status) {
      params = params.set('status', request.status);
    }
    if (request.notYetBilledOnly) {
      params = params.set('notYetBilledOnly', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<ReceiveOrderTrackResult>('/api/statements/receive-order-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/receive-order-track-report/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
