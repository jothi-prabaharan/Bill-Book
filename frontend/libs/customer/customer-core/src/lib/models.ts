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
