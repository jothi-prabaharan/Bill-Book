import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { CollectionBankAccountChoice } from '../invoice-dn-payment-collection/invoice-dn-payment-collection.models';
import { BillDnPaymentRequest, BillDnPaymentResult } from './bill-dn-payment.models';

/**
 * The Bill/DN Payment Report's own endpoint (TK-238), and the two lists its filters
 * offer: the branch's vendors and its bank accounts. Owns no state and touches no browser global,
 * so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BillDnPaymentService {
  private readonly http = inject(HttpClient);

  /** A page of the period's payments to vendors with the bills each settled, and the totals of every match. */
  async run(request: BillDnPaymentRequest): Promise<BillDnPaymentResult> {
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
        .get<BillDnPaymentResult>('/api/statements/bill-dn-payment-report', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/bill-dn-payment-report/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's bank accounts, for the bank account filter. */
  async bankAccounts(): Promise<CollectionBankAccountChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<CollectionBankAccountChoice[]>('/api/statements/bill-dn-payment-report/bank-accounts')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
