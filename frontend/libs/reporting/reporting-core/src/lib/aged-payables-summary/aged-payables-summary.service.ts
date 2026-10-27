import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesSummaryRequest, AgedPayablesSummaryResult, AgedPayablesVendorChoice } from './aged-payables-summary.models';

/**
 * The Aged Payables Summary's own endpoint (TK-233), and the one list its filter offers: the branch's
 * vendors. Owns no state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class AgedPayablesSummaryService {
  private readonly http = inject(HttpClient);

  /** What the branch owed each vendor on a date, by how overdue it was. */
  async run(request: AgedPayablesSummaryRequest): Promise<AgedPayablesSummaryResult> {
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
      this.http.get<AgedPayablesSummaryResult>('/api/statements/aged-payables-summary', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/aged-payables-summary/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
