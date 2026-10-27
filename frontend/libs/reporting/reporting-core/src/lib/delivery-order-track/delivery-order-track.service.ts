import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { DeliveryOrderTrackRequest, DeliveryOrderTrackResult } from './delivery-order-track.models';

/**
 * The Delivery Order Track Report's own endpoint (TK-232), and the list its customer filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class DeliveryOrderTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's challans with their billing and lines, the counts for the tabs and the totals of every match. */
  async run(request: DeliveryOrderTrackRequest): Promise<DeliveryOrderTrackResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.progress) {
      params = params.set('progress', request.progress);
    }
    if (request.challanType) {
      params = params.set('challanType', request.challanType);
    }
    if (request.notInvoicedOnly) {
      params = params.set('notInvoicedOnly', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<DeliveryOrderTrackResult>('/api/statements/delivery-order-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/delivery-order-track-report/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
