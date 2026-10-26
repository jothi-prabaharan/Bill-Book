import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  AgedReceivablesCustomerChoice,
  AgedReceivablesSummaryRequest,
  AgedReceivablesSummaryResult,
} from './aged-receivables-summary.models';

/**
 * The Aged Receivables Summary's own endpoint (TK-222), and the one list its filter
 * offers: the branch's customers. Owns no state and touches no browser global, so it
 * runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class AgedReceivablesSummaryService {
  private readonly http = inject(HttpClient);

  /** What each customer owed on a date, by how overdue it was. */
  async run(request: AgedReceivablesSummaryRequest): Promise<AgedReceivablesSummaryResult> {
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
    if (request.showZeroBalances) {
      params = params.set('showZeroBalances', true);
    }

    return new Promise((resolve, reject) =>
      this.http.get<AgedReceivablesSummaryResult>('/api/statements/aged-receivables-summary', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/aged-receivables-summary/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
