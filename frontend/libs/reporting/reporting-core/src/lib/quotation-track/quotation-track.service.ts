import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { QuotationTrackRequest, QuotationTrackResult } from './quotation-track.models';

/**
 * The Quotation Track Report's own endpoint (TK-231), and the list its customer filter offers. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class QuotationTrackService {
  private readonly http = inject(HttpClient);

  /** A page of the period's quotes with their outcomes, the counts for the tabs and the conversion rate. */
  async run(request: QuotationTrackRequest): Promise<QuotationTrackResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.outcome) {
      params = params.set('outcome', request.outcome);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<QuotationTrackResult>('/api/statements/quotation-track-report', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/quotation-track-report/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
