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

/**
 * One line, in the units the API takes — rupees and plain quantities.
 *
 * Built by `toApiLine`, never by hand: the grid works in integer paise and the
 * API does not, and the one place somebody forgets is a line off by a factor of
 * a hundred that still renders.
 */
export type SalesOrderLineRequest = ApiDocumentLine;

export interface VoidSalesOrderRequest {
  reason: string;
}

/**
 * Closing an order short: no more is coming, and what it still holds is
 * released.
 *
 * **Not a void.** A void says the order should not have existed; this says it
 * existed, was partly honoured, and both sides agreed to stop. The reason is
 * required — it is what tells a fulfilled order apart from a stopped one, which
 * the delivered quantities alone cannot say.
 */
export interface ShortCloseSalesOrderRequest {
  reason: string;
}

/** What one item has, holds, and can still promise. */
export interface StockAvailability {
  itemId: number;
  itemLabel?: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  /** False for something never stocked — a service line — rather than out of stock. */
  isTracked: boolean;
}

/** Turning an accepted quote into an order. The lines come from the quote. */
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

/**
 * One page of orders and how many matched in all.
 *
 * `total` is of the filtered set rather than the page, because that is what the
 * pager needs — a page that counted its own rows would say "50 of 50" on every
 * page of a thousand.
 */
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
  /** Set only when the order was stopped short rather than fulfilled. */
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

/** What the list screen may ask the server for. */
export interface SalesOrderListQuery {
  skip?: number;
  take?: number;
  status?: string;
  search?: string;
}

/**
 * The sales order endpoints.
 *
 * **Promises, not streams.** These are one-shot REST calls with no
 * cancellation, no retry and no composition, and `await` lets the caller wrap a
 * refusal in `try`/`catch` — which is what puts a rule's own words into the
 * message box instead of losing them in an error callback.
 */
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

  /** Confirms the order and reserves its stock. Nothing reaches the ledger. */
  async confirm(salesOrderId: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${salesOrderId}/confirm`, {}));
  }

  async voidOrder(salesOrderId: number, request: VoidSalesOrderRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${salesOrderId}/void`, request));
  }

  /** Closes the order short and releases whatever it was still holding. */
  async shortClose(
    salesOrderId: number,
    request: ShortCloseSalesOrderRequest,
  ): Promise<void> {
    return firstValueFrom(
      this.http.post<void>(`${this.apiUrl}/${salesOrderId}/short-close`, request),
    );
  }

  /**
   * What the items on the document can still be promised.
   *
   * **Advisory.** The figure is a moment old the instant it is drawn — another
   * till may confirm the last unit while somebody is still typing. What actually
   * decides is the guarded reservation taken on confirm, which no stale screen
   * can slip past.
   */
  async availability(itemIds: readonly number[]): Promise<StockAvailability[]> {
    if (itemIds.length === 0) {
      return [];
    }

    return firstValueFrom(
      this.http.post<StockAvailability[]>(`${this.apiUrl}/availability`, { itemIds }),
    );
  }
}
