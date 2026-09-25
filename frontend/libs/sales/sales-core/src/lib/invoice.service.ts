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
/** A document laid out by its print template, ready for the browser to print. */
export interface PrintedDocument {
  html: string;
  pageCount: number;
  /** Fields the template names that this document could not fill. They print as nothing. */
  unknownTags: string[];
  /** Null when the branch has no template and the standard layout was used. */
  printTemplateId: number | null;
}

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
  /** The IRP's reason code when the void cancels an IRN (TK-92); Other when not given. */
  cancelReason?: EInvoiceCancelReason;
}

// ---- E-invoicing (TK-92) ----

export type EInvoiceStatus = 'Pending' | 'Registered' | 'Failed' | 'Cancelled';

export type EInvoiceCancelReason = 'Duplicate' | 'DataEntryMistake' | 'OrderCancelled' | 'Other';

/** A document's registration at the IRP. */
export interface EInvoiceState {
  eInvoiceId: number;
  status: EInvoiceStatus;
  irn?: string | null;
  ackNo?: string | null;
  ackDate?: string | null;
  attempts: number;
  /** What went wrong, for the operator; null when nothing did. */
  message?: string | null;
  /** When the next automatic attempt is due, while still pending. */
  nextAttemptAt?: string | null;
}

/** The words a status is shown in. */
export const E_INVOICE_STATUS_LABELS: Record<EInvoiceStatus, string> = {
  Pending: 'IRN pending',
  Registered: 'IRN issued',
  Failed: 'IRN refused',
  Cancelled: 'IRN cancelled',
};

/** Refused, or not yet registered: not yet a valid tax invoice, so somebody should look. */
export function eInvoiceNeedsAttention(status: EInvoiceStatus | null | undefined): boolean {
  return status === 'Failed' || status === 'Pending';
}

/** An IRN can be cancelled for 24 hours from its acknowledgement; after that only a credit note corrects it. */
export function irnCancellable(state: EInvoiceState | null | undefined, now: Date = new Date()): boolean {
  if (!state || state.status !== 'Registered' || !state.ackDate) {
    return false;
  }
  return now.getTime() - new Date(state.ackDate).getTime() <= 24 * 60 * 60 * 1000;
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

  /** Where the invoice stands at the IRP; absent when it needs no IRN (TK-92). */
  eInvoiceStatus?: EInvoiceStatus | null;

  /**
   * What has been received against it, from Accounting's ledger.
   *
   * **Absent when the invoice has never posted** — a draft is not an unpaid
   * receivable — and absent when the ledger could not be read, in which case
   * the list still loads and simply does not claim to know.
   */
  paidAmount?: number;
  outstandingAmount?: number;

  /** `Unpaid` | `PartPaid` | `Paid`, derived from the ledger rather than stored. */
  settlementStatus?: string;
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
  /** Only invoices whose IRN was refused or is still pending (TK-92). */
  eInvoiceAttention?: boolean;
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
    if (query.eInvoiceAttention) {
      params = params.set('eInvoiceAttention', 'true');
    }

    return firstValueFrom(this.http.get<InvoiceListPage>(this.apiUrl, { params }));
  }

  async get(invoiceId: number): Promise<InvoiceView> {
    return firstValueFrom(this.http.get<InvoiceView>(`${this.apiUrl}/${invoiceId}`));
  }

  /** What posting would write to the ledger, without writing it. */
  /**
   * The invoice laid out by its print template, as HTML. Sales builds the data
   * and Printing lays it out with the branch's template; needs `sales.print`.
   */
  async print(invoiceId: number): Promise<PrintedDocument> {
    return firstValueFrom(
      this.http.get<PrintedDocument>(`${this.apiUrl}/${invoiceId}/print`),
    );
  }

  /**
   * The PDF archived when the invoice was posted (TK-22). A draft has none, and
   * the server answers 404 for it; needs `sales.print`.
   */
  async downloadPdf(invoiceId: number): Promise<Blob> {
    return firstValueFrom(
      this.http.get(`${this.apiUrl}/${invoiceId}/pdf`, { responseType: 'blob' }),
    );
  }

  /** The file name a downloaded PDF is saved under: the number, slashes as hyphens. */
  static pdfFileName(documentNo: string): string {
    return `${documentNo.replace(/[\\/:*?"<>|]/g, '-')}.pdf`;
  }

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

  /** The invoice's e-invoice, or null when it has none (TK-92). */
  async eInvoice(invoiceId: number): Promise<EInvoiceState | null> {
    try {
      return await firstValueFrom(this.http.get<EInvoiceState>(`${this.apiUrl}/${invoiceId}/e-invoice`));
    } catch (error: unknown) {
      if ((error as { status?: number })?.status === 404) {
        return null;
      }
      throw error;
    }
  }

  /** Registers the invoice at the IRP again now; needs `sales.einvoice`. */
  async retryEInvoice(invoiceId: number): Promise<EInvoiceState> {
    return firstValueFrom(this.http.post<EInvoiceState>(`${this.apiUrl}/${invoiceId}/e-invoice/retry`, {}));
  }

  async voidInvoice(invoiceId: number, request: VoidInvoiceRequest): Promise<void> {
    return firstValueFrom(this.http.post<void>(`${this.apiUrl}/${invoiceId}/void`, request));
  }
}
