import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ContactOption, ConvertedLead, Lead, Ticket, TicketMessage } from './models';
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

  /**
   * Converts a lead, down one of the two paths the API supports.
   *
   * **Exactly one, never both and never neither.** Passing a `contactId` links
   * that existing contact; passing none asks Contacts to create one from the
   * lead. The server refuses a request that names both or neither rather than
   * picking for the caller, so this never sends an empty body — an empty body
   * used to mean "convert somehow" and now means nothing at all.
   *
   * A `contactId` in another branch comes back 403. That is the point of the
   * check: the id alone never proved whose contact it was.
   */
  convertLead(id: string, contactId?: number): Promise<ConvertedLead> {
    return firstValueFrom(
      this.http.post<ConvertedLead>(
        `/api/leads/${id}/convert`,
        contactId === undefined ? { createContact: true } : { contactId },
      ),
    );
  }

  /**
   * Contacts to choose from, for the "link an existing one" path.
   *
   * Master's own list endpoint, so the results are already scoped to the
   * caller's branch — the picker cannot show a contact the conversion would
   * then be forbidden from using.
   */
  searchContacts(term: string): Promise<ContactOption[]> {
    const query = new URLSearchParams({ search: term });

    return firstValueFrom(this.http.get<ContactOption[]>(`/api/contacts?${query}`));
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
