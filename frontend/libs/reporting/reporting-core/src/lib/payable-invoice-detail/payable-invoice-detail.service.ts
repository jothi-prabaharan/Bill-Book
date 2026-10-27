import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import {
  PayableInvoiceCategoryChoice,
  PayableInvoiceDetailRequest,
  PayableInvoiceDetailResult,
  PayableInvoiceItemChoice,
} from './payable-invoice-detail.models';

/**
 * The Payable Invoice Detail's own endpoint (TK-236), and the three lists its filters offer: the
 * branch's vendors, its items and its item categories. Owns no state and touches no browser
 * global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class PayableInvoiceDetailService {
  private readonly http = inject(HttpClient);

  /** A page of the period's bills with their lines, the counts for the tabs and the totals of every match. */
  async run(request: PayableInvoiceDetailRequest): Promise<PayableInvoiceDetailResult> {
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
      this.http.get<PayableInvoiceDetailResult>('/api/statements/payable-invoice-detail', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<AgedPayablesVendorChoice[]>('/api/statements/payable-invoice-detail/vendors')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's active items, for the item filter. */
  async items(): Promise<PayableInvoiceItemChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceItemChoice[]>('/api/statements/payable-invoice-detail/items').subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's active item categories, for the category filter. */
  async itemCategories(): Promise<PayableInvoiceCategoryChoice[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<PayableInvoiceCategoryChoice[]>('/api/statements/payable-invoice-detail/item-categories')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
