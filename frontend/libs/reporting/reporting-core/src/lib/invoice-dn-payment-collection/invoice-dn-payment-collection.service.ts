import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import {
  CollectionBankAccountChoice,
  InvoiceDnPaymentCollectionRequest,
  InvoiceDnPaymentCollectionResult,
} from './invoice-dn-payment-collection.models';

/**
 * The Invoice/DN Payment Collection Report's own endpoint (TK-227), and the two lists its filters
 * offer: the branch's customers and its bank accounts. Owns no state and touches no browser global,
 * so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class InvoiceDnPaymentCollectionService {
  private readonly http = inject(HttpClient);

  /** A page of the period's receipts with their documents, and the totals of every match. */
  async run(request: InvoiceDnPaymentCollectionRequest): Promise<InvoiceDnPaymentCollectionResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.bankAccountId) {
      params = params.set('bankAccountId', request.bankAccountId);
    }
    if (request.paymentMethod) {
      params = params.set('paymentMethod', request.paymentMethod);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<InvoiceDnPaymentCollectionResult>('/api/statements/invoice-dn-payment-collection-report', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/invoice-dn-payment-collection-report/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's bank accounts, for the bank account filter. */
  async bankAccounts(): Promise<CollectionBankAccountChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<CollectionBankAccountChoice[]>('/api/statements/invoice-dn-payment-collection-report/bank-accounts')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
