import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedReceivablesCustomerChoice } from '../aged-receivables-summary/aged-receivables-summary.models';
import {
  ReceivableInvoiceCategoryChoice,
  ReceivableInvoiceDetailRequest,
  ReceivableInvoiceDetailResult,
  ReceivableInvoiceItemChoice,
} from './receivable-invoice-detail.models';

/**
 * The Receivable Invoice Detail's own endpoint (TK-225), and the three lists its filters offer: the
 * branch's customers, its items and its item categories. Owns no state and touches no browser
 * global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ReceivableInvoiceDetailService {
  private readonly http = inject(HttpClient);

  /** A page of the period's invoices with their lines, the counts for the tabs and the totals of every match. */
  async run(request: ReceivableInvoiceDetailRequest): Promise<ReceivableInvoiceDetailResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.itemId) {
      params = params.set('itemId', request.itemId);
    }
    if (request.itemCategoryId) {
      params = params.set('itemCategoryId', request.itemCategoryId);
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
      this.http.get<ReceivableInvoiceDetailResult>('/api/statements/receivable-invoice-detail', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's customers, for the customer filter. */
  async customers(): Promise<AgedReceivablesCustomerChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedReceivablesCustomerChoice[]>('/api/statements/receivable-invoice-detail/customers')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's active items, for the item filter. */
  async items(): Promise<ReceivableInvoiceItemChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<ReceivableInvoiceItemChoice[]>('/api/statements/receivable-invoice-detail/items').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's active item categories, for the category filter. */
  async itemCategories(): Promise<ReceivableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<ReceivableInvoiceCategoryChoice[]>('/api/statements/receivable-invoice-detail/item-categories')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
