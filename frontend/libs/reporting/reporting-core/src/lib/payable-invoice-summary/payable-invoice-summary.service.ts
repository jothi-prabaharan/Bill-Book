import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { PayableInvoiceCurrencyChoice, PayableInvoiceSummaryRequest, PayableInvoiceSummaryResult } from './payable-invoice-summary.models';

/**
 * The Payable Invoice Summary's own endpoint (TK-235), and the two lists its filters offer: the branch's
 * vendors and the currencies it has been billed in. Owns no state and touches no browser global, so it runs
 * in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class PayableInvoiceSummaryService {
  private readonly http = inject(HttpClient);

  /** A page of the period's bills, with the counts for the tabs and the totals of every match. */
  async run(request: PayableInvoiceSummaryRequest): Promise<PayableInvoiceSummaryResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.currencyCode) {
      params = params.set('currencyCode', request.currencyCode);
    }
    if (request.status) {
      params = params.set('status', request.status);
    }
    if (request.includeDebitNotes) {
      params = params.set('includeDebitNotes', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceSummaryResult>('/api/statements/payable-invoice-summary', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/payable-invoice-summary/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The currencies the branch has been billed in, for the currency filter. */
  async currencies(): Promise<PayableInvoiceCurrencyChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<PayableInvoiceCurrencyChoice[]>('/api/statements/payable-invoice-summary/currencies')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
