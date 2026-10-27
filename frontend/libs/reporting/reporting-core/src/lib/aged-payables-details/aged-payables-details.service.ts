import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { AgedPayablesDetailsRequest, AgedPayablesDetailsResult } from './aged-payables-details.models';

/**
 * The Aged Payables Details' own endpoint (TK-234), and the one list its filter offers: the branch's vendors.
 * Owns no state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class AgedPayablesDetailsService {
  private readonly http = inject(HttpClient);

  /** Each vendor's open documents on a date, by how overdue each was. */
  async run(request: AgedPayablesDetailsRequest): Promise<AgedPayablesDetailsResult> {
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
      this.http.get<AgedPayablesDetailsResult>('/api/statements/aged-payables-details', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/aged-payables-details/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
