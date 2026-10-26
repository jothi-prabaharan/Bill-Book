import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import { AgedReceivablesDetailRequest, AgedReceivablesDetailResult } from './aged-receivables-detail.models';

/**
 * The Aged Receivables Detail's own endpoint (TK-223), and the one list its filter offers: the
 * branch's customers. Owns no state and touches no browser global, so it runs in a mobile shell
 * too.
 */
@Injectable({ providedIn: 'root' })
export class AgedReceivablesDetailService {
  private readonly http = inject(HttpClient);

  /** Each customer's open documents on a date, by how overdue each was. */
  async run(request: AgedReceivablesDetailRequest): Promise<AgedReceivablesDetailResult> {
    let params = new HttpParams().set('asAtDate', request.asAtDate);
    if (request.ageBy) {
      params = params.set('ageBy', request.ageBy);
    }
    if (request.bucketDays) {
      params = params.set('bucketDays', request.bucketDays);
    }
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }

    return new Promise((resolve, reject) =>
      this.http.get<AgedReceivablesDetailResult>('/api/statements/aged-receivables-detail', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/aged-receivables-detail/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
