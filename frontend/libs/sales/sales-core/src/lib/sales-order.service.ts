import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiDocumentLine } from './document-line-scale';

/**
 * A sales order as the screen sends it.
 *
 * **No totals and no tax.** The server computes every figure from the lines at
 * the rates in force on the document's date; a caller free to send its own
 * would be free to save a document whose foot disagrees with its body.
 */
export interface SaveSalesOrderRequest {
  documentDate: string;
  contactId: number;
  quoteId?: number;
  deliveryDate?: string;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  billingAddress?: string;
  shippingAddress?: string;
  currencyCode?: string;
  exchangeRate?: number;
  notes?: string;
  termsAndConditions?: string;
  lines: SalesOrderLineRequest[];
}

export type SalesOrderLineRequest = ApiDocumentLine;

export interface VoidSalesOrderRequest {
  reason: string;
}

export interface ShortCloseSalesOrderRequest {
  reason: string;
}

/** Requests partial or full invoice-based fulfillment of a confirmed order. */
export interface FulfillSalesOrderRequest {
  documentDate?: string;
  dueDate?: string;
  paymentTermId?: number;
  placeOfSupplyStateCode?: string;
  notes?: string;
  /** Empty means fulfill every remaining line. */
  lines?: FulfillSalesOrderLineRequest[];
}

export interface FulfillSalesOrderLineRequest {
  salesOrderDetailId: number;
  quantity: number;
}

export interface FulfilledSalesOrderLine {
  salesOrderDetailId: number;
  orderedQuantity: number;
  previouslyInvoicedQuantity: number;
  fulfilledQuantity: number;
  remainingQuantity: number;
}

export interface FulfillSalesOrderResult {
  salesOrderId: number;
  invoiceId: number;
  status: string;
  lines: FulfilledSalesOrderLine[];
}

/** What one item has, holds, and can still promise. */
export interface StockAvailability {
  itemId: number;
  itemLabel?: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  isTracked: boolean;
}

export interface CreateOrderFromQuoteRequest {
  documentDate?: string;
  deliveryDate?: string;
  placeOfSupplyStateCode?: string;
  notes?: string;
}

export interface SalesOrderListItem {
  salesOrderId: number;
  documentNo: string;
  documentDate: string;
  quoteId?: number;
  deliveryDate?: string;
  fulfilmentStatus: string;
  contactId: number;
  contactName?: string;
  contactCode?: string;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  invoicedDocumentId?: number;
}

export interface SalesOrderListPage {
  total: number;
  skip: number;
  take: number;
  rows: SalesOrderListItem[];
}

export interface SalesOrderView extends SalesOrderListItem {
  contactGstin?: string;
  placeOfSupplyStateId: number;
  billingAddress?: string;
  shippingAddress?: string;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  totalAmountBase: number;
  notes?: string;
  termsAndConditions?: string;
  postedAt?: string;
  voidedAt?: string;
  voidReason?: string;
  shortCloseReason?: string;
  lines: SalesOrderLineView[];
}

export interface SalesOrderLineView extends ApiDocumentLine {
  salesOrderDetailId: number;
  lineNumber: number;
  itemLabel?: string;
  baseQuantity: number;
  reservedQuantity: number;
  deliveredQuantity: number;
  grossAmount: number;
  taxableAmount: number;
  taxAmount: number;
  lineTotal: number;
  taxes: SalesOrderLineTaxView[];
}

export interface SalesOrderLineTaxView {
  salesOrderDetailTaxId: number;
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

export interface SalesOrderListQuery {
  skip?: number;
  take?: number;
  status?: string;
  search?: string;
}

@Injectable({ providedIn: 'root' })
export class SalesOrderService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/sales/sales-orders';

  async list(query: SalesOrderListQuery = {}): Promise<SalesOrderListPage> {
    let params = new HttpParams()
      .set('skip', String(query.skip ?? 0))
      .set('take', String(query.take ?? 50));

    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }

    return firstValueFrom(this.http.get<SalesOrderListPage>(this.apiUrl, { params }));
  }

  async get(salesOrderId: number): Promise<SalesOrderView> {
    return firstValueFrom(this.http.get<SalesOrderView>(`${this.apiUrl}/${salesOrderId}`));
  }

  async create(request: SaveSalesOrderRequest): Promise<{ salesOrderId: number }> {
    return firstValueFrom(this.http.post<{ salesOrderId: number }>(this.apiUrl, request));
  }

  async createFromQuote(
    quoteId: number,
    request: CreateOrderFromQuoteRequest,
  ): Promise<{ salesOrderId: number }> {
    return firstValueFrom(
      this.http.post<{ salesOrderId: number }>(`${this.apiUrl}/from-quote/${quoteId}`, request),
    );
  }

  async update(salesOrderId: number, request: SaveSalesOrderRequest): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.apiUrl}/${salesOrderId}`, request));
  }

  async confirm(salesOrderId: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${salesOrderId}/confirm`, {}));
  }

  /**
   * Creates and posts an invoice for some or all remaining order quantity.
   * The server validates the remaining quantity and performs the stock/ledger
   * work through the existing invoice pipeline.
   */
  async fulfill(
    salesOrderId: number,
    request: FulfillSalesOrderRequest,
  ): Promise<FulfillSalesOrderResult> {
    return firstValueFrom(
      this.http.post<FulfillSalesOrderResult>(
        `${this.apiUrl}/${salesOrderId}/fulfill`,
        request,
      ),
    );
  }

  async voidOrder(salesOrderId: number, request: VoidSalesOrderRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${salesOrderId}/void`, request));
  }

  async shortClose(
    salesOrderId: number,
    request: ShortCloseSalesOrderRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${salesOrderId}/short-close`, request),
    );
  }

  async availability(itemIds: readonly number[]): Promise<StockAvailability[]> {
    if (itemIds.length === 0) {
      return [];
    }

    return firstValueFrom(
      this.http.post<StockAvailability[]>(`${this.apiUrl}/availability`, { itemIds }),
    );
  }
}
