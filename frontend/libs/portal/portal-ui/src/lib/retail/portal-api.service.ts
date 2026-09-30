import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  PortalInvoiceDetail,
  PortalInvoiceItem,
  PortalPayment,
  PortalQuoteItem,
  PortalStatement,
  PortalSummary,
  PortalTicketDetail,
  PortalTicketItem,
} from './portal.models';

/**
 * The customer portal's reads (TK-95). Every route is under `/api/portal/`, so
 * the portal interceptor sends the contact's session token, and each service
 * answers for that contact only.
 */
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);

  summary(): Promise<PortalSummary> {
    return firstValueFrom(this.http.get<PortalSummary>('/api/portal/summary'));
  }

  invoices(): Promise<PortalInvoiceItem[]> {
    return firstValueFrom(this.http.get<PortalInvoiceItem[]>('/api/portal/invoices'));
  }

  invoice(id: number): Promise<PortalInvoiceDetail> {
    return firstValueFrom(this.http.get<PortalInvoiceDetail>(`/api/portal/invoices/${id}`));
  }

  invoicePdf(id: number): Promise<Blob> {
    return firstValueFrom(this.http.get(`/api/portal/invoices/${id}/pdf`, { responseType: 'blob' }));
  }

  quotes(): Promise<PortalQuoteItem[]> {
    return firstValueFrom(this.http.get<PortalQuoteItem[]>('/api/portal/quotes'));
  }

  /** Accepts or declines a quote, once (TK-96). */
  answerQuote(id: number, answer: 'accept' | 'reject', name: string, note: string | null): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/portal/quotes/${id}/${answer}`, { name, note }));
  }

  tickets(): Promise<PortalTicketItem[]> {
    return firstValueFrom(this.http.get<PortalTicketItem[]>('/api/portal/tickets'));
  }

  ticket(id: number): Promise<PortalTicketDetail> {
    return firstValueFrom(this.http.get<PortalTicketDetail>(`/api/portal/tickets/${id}`));
  }

  raiseTicket(subject: string, description: string | null): Promise<{ ticketId: number }> {
    return firstValueFrom(this.http.post<{ ticketId: number }>('/api/portal/tickets', { subject, description }));
  }

  replyToTicket(id: number, body: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/portal/tickets/${id}/messages`, { body }));
  }

  /** Opens an online payment for chosen invoices and anything extra on account (TK-98). */
  startPayment(invoices: { invoiceId: number; amount: number }[], amount: number): Promise<{ onlinePaymentId: number; checkoutUrl: string }> {
    return firstValueFrom(
      this.http.post<{ onlinePaymentId: number; checkoutUrl: string }>('/api/portal/payments', { invoices, amount }),
    );
  }

  payment(id: number): Promise<PortalPayment> {
    return firstValueFrom(this.http.get<PortalPayment>(`/api/portal/payments/${id}`));
  }

  /** The sandbox gateway's checkout: pays or fails the payment through the verified-callback path (D-25). */
  sandboxCheckout(id: number, succeed: boolean): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/portal/payments/${id}/sandbox-checkout`, { succeed }));
  }

  statement(fromDate?: string, toDate?: string): Promise<PortalStatement> {
    let params = new HttpParams();
    if (fromDate) params = params.set('fromDate', fromDate);
    if (toDate) params = params.set('toDate', toDate);
    return firstValueFrom(this.http.get<PortalStatement>('/api/portal/statements', { params }));
  }
}
