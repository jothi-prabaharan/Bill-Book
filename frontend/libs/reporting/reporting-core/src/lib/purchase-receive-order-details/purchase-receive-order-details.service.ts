import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AgedPayablesVendorChoice } from '../aged-payables-summary/aged-payables-summary.models';
import { PayableInvoiceItemChoice } from '../payable-invoice-detail/payable-invoice-detail.models';
import { SalesAnalysisWarehouseChoice } from '../sales-analysis/sales-analysis.models';
import {
  PurchaseReceiveOrderDetailsOrderChoice,
  PurchaseReceiveOrderDetailsRequest,
  PurchaseReceiveOrderDetailsResult,
} from './purchase-receive-order-details.models';

const BASE = '/api/statements/purchase-receive-order-details';

/**
 * The Purchase Receive Order Details' own endpoint (TK-241), and the lists its vendor, item, warehouse and
 * purchase order filters offer. Owns no state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class PurchaseReceiveOrderDetailsService {
  private readonly http = inject(HttpClient);

  /** A page of the period's goods receipts with their lines, and the totals of every receipt the filters match. */
  async run(request: PurchaseReceiveOrderDetailsRequest): Promise<PurchaseReceiveOrderDetailsResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.itemId) {
      params = params.set('itemId', request.itemId);
    }
    if (request.warehouseId) {
      params = params.set('warehouseId', request.warehouseId);
    }
    if (request.purchaseOrderId) {
      params = params.set('purchaseOrderId', request.purchaseOrderId);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<PurchaseReceiveOrderDetailsResult>(BASE, { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's vendors, for the vendor filter. */
  async vendors(): Promise<AgedPayablesVendorChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<AgedPayablesVendorChoice[]>(`${BASE}/vendors`).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's items, for the item filter. */
  async items(): Promise<PayableInvoiceItemChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PayableInvoiceItemChoice[]>(`${BASE}/items`).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's warehouses, for the warehouse filter. */
  async warehouses(): Promise<SalesAnalysisWarehouseChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<SalesAnalysisWarehouseChoice[]>(`${BASE}/warehouses`).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The newest purchase orders a posted receipt was raised against, for the purchase order filter. */
  async purchaseOrders(): Promise<PurchaseReceiveOrderDetailsOrderChoice[]> {
    return new Promise((resolve, reject) =>
      this.http.get<PurchaseReceiveOrderDetailsOrderChoice[]>(`${BASE}/purchase-orders`).subscribe({ next: resolve, error: reject }),
    );
  }
}
