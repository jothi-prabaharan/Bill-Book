import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  Audience,
  AudiencePreviewResponse,
  AudienceRules,
  Campaign,
  CampaignLink,
  CampaignRecipient,
  ConsentStatus,
  ContactOption,
  ConvertedLead,
  EmailConsent,
  EmailTemplate,
  Lead,
  SaveAudienceRequest,
  SaveCampaignRequest,
  SaveConsentRequest,
  SaveEmailTemplateRequest,
  ScheduleCampaignRequest,
  Ticket,
  TicketMessage
} from './models';
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

  /** A staff reply, or with `isInternal` a note the customer never sees (TK-97). */
  createTicketMessage(ticketId: string, body: string, isInternal = false): Promise<TicketMessage> {
    return firstValueFrom(this.http.post<TicketMessage>(`/api/tickets/${ticketId}/messages`, { body, isInternal }));
  }

  // Email Templates
  getEmailTemplates(): Promise<EmailTemplate[]> {
    return firstValueFrom(this.http.get<EmailTemplate[]>('/api/campaigns/templates'));
  }

  getEmailTemplate(id: number): Promise<EmailTemplate> {
    return firstValueFrom(this.http.get<EmailTemplate>(`/api/campaigns/templates/${id}`));
  }

  createEmailTemplate(request: SaveEmailTemplateRequest): Promise<EmailTemplate> {
    return firstValueFrom(this.http.post<EmailTemplate>('/api/campaigns/templates', request));
  }

  updateEmailTemplate(id: number, request: SaveEmailTemplateRequest): Promise<EmailTemplate> {
    return firstValueFrom(this.http.put<EmailTemplate>(`/api/campaigns/templates/${id}`, request));
  }

  deleteEmailTemplate(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/campaigns/templates/${id}`));
  }

  // Audiences
  getAudiences(): Promise<Audience[]> {
    return firstValueFrom(this.http.get<Audience[]>('/api/campaigns/audiences'));
  }

  getAudience(id: number): Promise<Audience> {
    return firstValueFrom(this.http.get<Audience>(`/api/campaigns/audiences/${id}`));
  }

  createAudience(request: SaveAudienceRequest): Promise<Audience> {
    return firstValueFrom(this.http.post<Audience>('/api/campaigns/audiences', request));
  }

  updateAudience(id: number, request: SaveAudienceRequest): Promise<Audience> {
    return firstValueFrom(this.http.put<Audience>(`/api/campaigns/audiences/${id}`, request));
  }

  deleteAudience(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/campaigns/audiences/${id}`));
  }

  previewAudience(rules: AudienceRules): Promise<AudiencePreviewResponse> {
    return firstValueFrom(this.http.post<AudiencePreviewResponse>('/api/campaigns/audiences/preview', rules));
  }

  countAudience(rules: AudienceRules): Promise<{ count: number; suppressedCount: number }> {
    return firstValueFrom(this.http.post<{ count: number; suppressedCount: number }>('/api/campaigns/audiences/count', rules));
  }

  getAudienceRecipients(id: number): Promise<AudiencePreviewResponse> {
    return firstValueFrom(this.http.get<AudiencePreviewResponse>(`/api/campaigns/audiences/${id}/recipients`));
  }

  // Email Consents
  getEmailConsents(search?: string, status?: ConsentStatus, page = 1, pageSize = 50): Promise<EmailConsent[]> {
    const params = new URLSearchParams();
    if (search) params.set('search', search);
    if (status) params.set('status', status);
    params.set('page', page.toString());
    params.set('pageSize', pageSize.toString());

    return firstValueFrom(this.http.get<EmailConsent[]>(`/api/campaigns/consent?${params.toString()}`));
  }

  getEmailConsent(email: string): Promise<EmailConsent> {
    return firstValueFrom(this.http.get<EmailConsent>(`/api/campaigns/consent/${encodeURIComponent(email)}`));
  }

  recordEmailConsent(request: SaveConsentRequest): Promise<EmailConsent> {
    return firstValueFrom(this.http.post<EmailConsent>('/api/campaigns/consent', request));
  }

  // Public Unsubscribe
  verifyUnsubscribeToken(token: string): Promise<{ email: string; message: string }> {
    return firstValueFrom(this.http.get<{ email: string; message: string }>(`/api/public/unsubscribe/${encodeURIComponent(token)}`));
  }

  unsubscribeByToken(token: string): Promise<{ email: string; message: string }> {
    return firstValueFrom(this.http.post<{ email: string; message: string }>(`/api/public/unsubscribe/${encodeURIComponent(token)}`, {}));
  }

  // Campaigns
  getCampaigns(): Promise<Campaign[]> {
    return firstValueFrom(this.http.get<Campaign[]>('/api/campaigns'));
  }

  getCampaign(id: number): Promise<Campaign> {
    return firstValueFrom(this.http.get<Campaign>(`/api/campaigns/${id}`));
  }

  createCampaign(request: SaveCampaignRequest): Promise<Campaign> {
    return firstValueFrom(this.http.post<Campaign>('/api/campaigns', request));
  }

  updateCampaign(id: number, request: SaveCampaignRequest): Promise<Campaign> {
    return firstValueFrom(this.http.put<Campaign>(`/api/campaigns/${id}`, request));
  }

  deleteCampaign(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`/api/campaigns/${id}`));
  }

  scheduleCampaign(id: number, request: ScheduleCampaignRequest): Promise<Campaign> {
    return firstValueFrom(this.http.post<Campaign>(`/api/campaigns/${id}/schedule`, request));
  }

  sendCampaignNow(id: number): Promise<Campaign> {
    return firstValueFrom(this.http.post<Campaign>(`/api/campaigns/${id}/send-now`, {}));
  }

  cancelCampaign(id: number): Promise<Campaign> {
    return firstValueFrom(this.http.post<Campaign>(`/api/campaigns/${id}/cancel`, {}));
  }

  getCampaignRecipients(id: number, page = 1, pageSize = 50): Promise<CampaignRecipient[]> {
    return firstValueFrom(this.http.get<CampaignRecipient[]>(`/api/campaigns/${id}/recipients?page=${page}&pageSize=${pageSize}`));
  }

  getCampaignLinks(id: number): Promise<CampaignLink[]> {
    return firstValueFrom(this.http.get<CampaignLink[]>(`/api/campaigns/${id}/links`));
  }
}


