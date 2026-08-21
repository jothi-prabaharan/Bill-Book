import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiDocumentLine } from './document-line-scale';

// ---- Request models ----

/**
 * An invoice as the screen sends it.
 *
 * **No totals and no tax.** The server computes every figure from the lines at
 * the rates in force on the document's date. This is the document a GST return
 * is filed from, so a caller free to send its own tax is one that can file the
 * wrong return.
 */
export interface SaveInvoiceRequest {
  documentDate: string;
  contactId: number;
  quoteId?: number;
  salesOrderId?: number;
  deliveryChallanId?: number;
  paymentTermId?: number;
  /** Required on an INV. A POS sale takes a till instead. */
  dueDate?: string;
  tillId?: number;
  cashierUserId?: string;
  paymentMode?: string;
  tenderedAmount?: number;
  changeAmount?: number;
  contactGstin?: string;
  placeOfSupplyStateCode?: string;
  billingAddress?: string;
  shippingAddress?: string;
  currencyCode?: string;
  exchangeRate?: number;
  notes?: string;
  termsAndConditions?: string;
  lines: InvoiceLineRequest[];
}

/**
 * One line, in the units the API takes — rupees and plain quantities.
 *
 * Built by `toApiLine`, never by hand: the grid works in integer paise and the
 * API does not, and the one place somebody forgets is a line off by a factor of
 * a hundred that still renders.
 */
export interface InvoiceLineRequest extends ApiDocumentLine {
  /** The order line this came from, when the invoice was raised from one. */
  salesOrderDetailId?: number | null;
}

export interface VoidInvoiceRequest {
  reason: string;
}

/** Invoicing a confirmed sales order. The lines come from the order. */
export interface CreateInvoiceFromOrderRequest {
  documentDate?: string;
  dueDate?: string;
  paymentTermId?: number;
  placeOfSupplyStateCode?: string;
  notes?: string;
}

// ---- Response / view models ----

export interface InvoiceListItem {
  invoiceId: number;
  documentNo: string;
  documentDate: string;
  dueDate?: string;
  quoteId?: number;
  salesOrderId?: number;
  deliveryChallanId?: number;
  contactId: number;
  contactName?: string;
  contactCode?: string;
  currencyCode: string;
  taxableAmount: number;
  totalAmount: number;
  status: string;
  isInterState: boolean;
  /** Zero unless the invoice is posted and past its due date. */
  daysOverdue: number;
  paymentMode?: string;
}

/**
 * One page of invoices and how many matched in all.
 *
 * `total` is of the filtered set rather than the page, because that is what the
 * pager needs.
 */
export interface InvoiceListPage {
  total: number;
  skip: number;
  take: number;
  rows: InvoiceListItem[];
}

export interface InvoiceView extends InvoiceListItem {
  paymentTermId?: number;
  tillId?: number;
  cashierUserId?: string;
  tenderedAmount?: number;
  changeAmount?: number;
  contactGstin?: string;
  billingAddress?: string;
  shippingAddress?: string;
  placeOfSupplyStateId: number;
  exchangeRate: number;
  subTotal: number;
  discountAmount: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  cessAmount: number;
  roundOffAmount: number;
  notes?: string;
  termsAndConditions?: string;
  postedAt?: string;
  voidedAt?: string;
  voidReason?: string;
  lines: InvoiceLineView[];
}

export interface InvoiceLineView extends ApiDocumentLine {
  invoiceDetailId: number;
  lineNumber: number;
  itemLabel?: string;
  baseQuantity: number;
  returnedQuantity: number;
  salesOrderDetailId?: number;
  grossAmount: number;
  taxableAmount: number;
  taxAmount: number;
  lineTotal: number;
  taxes: InvoiceLineTaxView[];
}

export interface InvoiceLineTaxView {
  invoiceDetailTaxId: number;
  taxComponent: string;
  rate: number;
  taxableAmount: number;
  amount: number;
  amountBase: number;
}

/** One leg of what posting this invoice would write to the ledger. */
export interface GlPreviewLeg {
  accountSystemName: string;
  debitAmount: number;
  creditAmount: number;
  narration?: string;
}

export interface GlPreviewResult {
  legs: GlPreviewLeg[];
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
}

/** What the list screen may ask the server for. */
export interface InvoiceListQuery {
  skip?: number;
  take?: number;
  status?: string;
  search?: string;
  from?: string;
  to?: string;
  overdueOnly?: boolean;
}

// ---- Service ----

/**
 * The invoice endpoints.
 *
 * **Promises, not streams.** These are one-shot REST calls with no
 * cancellation, no retry and no composition, and `await` lets the caller wrap a
 * refusal in `try`/`catch` — which is what puts a rule's own words into the
 * message box instead of losing them in an error callback.
 */
@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/sales/invoices';

  async list(query: InvoiceListQuery = {}): Promise<InvoiceListPage> {
    let params = new HttpParams()
      .set('skip', String(query.skip ?? 0))
      .set('take', String(query.take ?? 50));

    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    if (query.overdueOnly) {
      params = params.set('overdueOnly', 'true');
    }

    return firstValueFrom(this.http.get<InvoiceListPage>(this.apiUrl, { params }));
  }

  async get(invoiceId: number): Promise<InvoiceView> {
    return firstValueFrom(this.http.get<InvoiceView>(`${this.apiUrl}/${invoiceId}`));
  }

  /** What posting would write to the ledger, without writing it. */
  async previewGl(invoiceId: number): Promise<GlPreviewResult> {
    return firstValueFrom(
      this.http.get<GlPreviewResult>(`${this.apiUrl}/${invoiceId}/gl-preview`),
    );
  }

  async create(request: SaveInvoiceRequest): Promise<{ invoiceId: number }> {
    return firstValueFrom(this.http.post<{ invoiceId: number }>(this.apiUrl, request));
  }

  async createFromSalesOrder(
    salesOrderId: number,
    request: CreateInvoiceFromOrderRequest,
  ): Promise<{ invoiceId: number }> {
    return firstValueFrom(
      this.http.post<{ invoiceId: number }>(
        `${this.apiUrl}/from-sales-order/${salesOrderId}`,
        request,
      ),
    );
  }

  async update(invoiceId: number, request: SaveInvoiceRequest): Promise<void> {
    return firstValueFrom(this.http.put<void>(`${this.apiUrl}/${invoiceId}`, request));
  }

  /** Posts the double entry, issues the stock, and freezes the invoice. */
  async post(invoiceId: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${invoiceId}/post`, {}));
  }

  async voidInvoice(invoiceId: number, request: VoidInvoiceRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${invoiceId}/void`, request));
  }
}
