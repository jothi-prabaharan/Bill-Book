export enum LeadSource {
  Website = 'Website',
  Referral = 'Referral',
  WalkIn = 'WalkIn',
  Other = 'Other'
}

export enum LeadStatus {
  New = 'New',
  Contacted = 'Contacted',
  Qualified = 'Qualified',
  Converted = 'Converted',
  Lost = 'Lost'
}

export interface Lead {
  leadId: string;
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  source: LeadSource;
  status: LeadStatus;
  convertedContactId?: string;
}

export enum TicketPriority {
  Low = 'Low',
  Medium = 'Medium',
  High = 'High',
  Urgent = 'Urgent'
}

export enum TicketStatus {
  Open = 'Open',
  InProgress = 'InProgress',
  Resolved = 'Resolved',
  Closed = 'Closed'
}

export interface Ticket {
  ticketId: string;
  subject: string;
  description: string;
  priority: TicketPriority;
  status: TicketStatus;
  contactId: string;
}

export interface TicketMessage {
  id: string;
  ticketId: string;
  body: string;
  authorType: 'Contact' | 'User';
  createdAt: string;
}

/**
 * What a conversion gives back — the contact it linked or made, so the screen
 * can go straight to it rather than making the user find it.
 */
export interface ConvertedLead {
  leadId: number;
  contactId: number;
  /** Null when an existing contact was linked: only a new one reports its code. */
  contactCode: string | null;
  convertedAt: string;
}

/** One contact in the picker. A slice of Master's list row, not the whole of it. */
export interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  gstin: string | null;
}
