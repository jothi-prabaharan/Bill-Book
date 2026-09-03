import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Lead, Ticket, TicketMessage } from './models';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);

  // Leads
  getLeads(): Promise<Lead[]> {
    return firstValueFrom(this.http.get<Lead[]>('/api/leads'));
  }

  getLead(id: string): Promise<Lead> {
    return firstValueFrom(this.http.get<Lead>(`/api/leads/${id}`));
  }

  createLead(lead: Partial<Lead>): Promise<Lead> {
    return firstValueFrom(this.http.post<Lead>('/api/leads', lead));
  }

  convertLead(id: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/leads/${id}/convert`, {}));
  }

  // Tickets
  getTickets(): Promise<Ticket[]> {
    return firstValueFrom(this.http.get<Ticket[]>('/api/tickets'));
  }

  getTicket(id: string): Promise<Ticket> {
    return firstValueFrom(this.http.get<Ticket>(`/api/tickets/${id}`));
  }

  createTicket(ticket: Partial<Ticket>): Promise<Ticket> {
    return firstValueFrom(this.http.post<Ticket>('/api/tickets', ticket));
  }

  // Ticket Messages
  getTicketMessages(ticketId: string): Promise<TicketMessage[]> {
    return firstValueFrom(this.http.get<TicketMessage[]>(`/api/tickets/${ticketId}/messages`));
  }

  createTicketMessage(ticketId: string, body: string): Promise<TicketMessage> {
    return firstValueFrom(this.http.post<TicketMessage>(`/api/tickets/${ticketId}/messages`, { body }));
  }
}
