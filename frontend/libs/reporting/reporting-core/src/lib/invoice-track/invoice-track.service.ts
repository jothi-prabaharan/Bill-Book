import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { InvoiceTrackRequest, InvoiceTrackResult } from './invoice-track.models';

/**
 * The Invoice Track Report's own endpoint (TK-226), and the list its customer filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class InvoiceTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's invoices with where each stands, the counts for the tabs and the totals of the posted ones. */
  async run(request: InvoiceTrackRequest): Promise<InvoiceTrackResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.status) {
      params = params.set('status', request.status);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<InvoiceTrackResult>('/api/statements/invoice-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/invoice-track-report/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
