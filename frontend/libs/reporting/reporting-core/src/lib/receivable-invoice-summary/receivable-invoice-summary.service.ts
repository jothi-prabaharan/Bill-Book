import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import {
  ReceivableInvoiceCurrencyChoice,
  ReceivableInvoiceSummaryRequest,
  ReceivableInvoiceSummaryResult,
} from './receivable-invoice-summary.models';

/**
 * The Receivable Invoice Summary's own endpoint (TK-224), and the two lists its filters offer: the
 * branch's customers and the currencies it has invoiced in. Owns no state and touches no browser
 * global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ReceivableInvoiceSummaryService {
  private readonly http = inject(HttpClient);

  /** A page of the period's invoices, with the counts for the tabs and the totals of every match. */
  async run(request: ReceivableInvoiceSummaryRequest): Promise<ReceivableInvoiceSummaryResult> {
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
    if (request.includeCreditNotes) {
      params = params.set('includeCreditNotes', true);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<ReceivableInvoiceSummaryResult>('/api/statements/receivable-invoice-summary', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/receivable-invoice-summary/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The currencies the branch has invoiced in, for the currency filter. */
  async currencies(): Promise<ReceivableInvoiceCurrencyChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<ReceivableInvoiceCurrencyChoice[]>('/api/statements/receivable-invoice-summary/currencies')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
