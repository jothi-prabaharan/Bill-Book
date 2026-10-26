import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { SalesOrderTrackRequest, SalesOrderTrackResult } from './sales-order-track.models';

/**
 * The Sales Order Track Report's own endpoint (TK-230), and the list its customer filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class SalesOrderTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's orders with their progress and lines, the counts for the tabs and the totals of every match. */
  async run(request: SalesOrderTrackRequest): Promise<SalesOrderTrackResult> {
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
      this.http.get<SalesOrderTrackResult>('/api/statements/sales-order-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/sales-order-track-report/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
