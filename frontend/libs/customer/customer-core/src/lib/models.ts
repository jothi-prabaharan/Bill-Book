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
  /**
   * A `con.Contacts` id, which is an `int` everywhere else in the product —
   * every picker publishes one and the API takes one. It was declared `string`
   * here, and the ticket form's `as any` on the way to `createTicket` is what
   * kept the two from ever meeting.
   */
  contactId: number;
}

export interface TicketMessage {
  id: string;
  ticketId: string;
  body: string;
  authorType: 'Contact' | 'User';
  /** A note for colleagues; the client portal never shows it (TK-97). */
  isInternal?: boolean;
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

export enum ConsentStatus {
  Subscribed = 'Subscribed',
  Unsubscribed = 'Unsubscribed',
  Bounced = 'Bounced'
}

export enum ConsentSource {
  UnsubscribeLink = 'UnsubscribeLink',
  Staff = 'Staff',
  Import = 'Import',
  Bounce = 'Bounce'
}

export interface EmailTemplate {
  emailTemplateId: number;
  name: string;
  subject: string;
  htmlBody: string;
  textBody?: string;
  isActive: boolean;
  createdAt?: string;
  modifiedAt?: string;
}

export interface SaveEmailTemplateRequest {
  name: string;
  subject: string;
  htmlBody: string;
  textBody?: string;
  isActive: boolean;
}

export interface LeadRule {
  statuses?: LeadStatus[];
  sources?: LeadSource[];
  createdFrom?: string;
  createdTo?: string;
}

export interface ContactRule {
  isCustomer?: boolean;
  isVendor?: boolean;
  state?: string;
  city?: string;
  category?: string;
}

export interface PurchaseHistoryRule {
  lastInvoiceBefore?: string;
  lastInvoiceAfter?: string;
  salesMinAmount?: number;
  salesMaxAmount?: number;
  salesPeriodFrom?: string;
  salesPeriodTo?: string;
  itemCategoryId?: number;
}

export interface ManualListRule {
  emails?: string[];
  contactIds?: number[];
  leadIds?: number[];
}

export interface AudienceRules {
  leads?: LeadRule;
  contacts?: ContactRule;
  purchaseHistory?: PurchaseHistoryRule;
  customFields?: Record<string, string>;
  manualInclusions?: ManualListRule;
  manualExclusions?: ManualListRule;
}

export interface Audience {
  audienceId: number;
  name: string;
  rules: AudienceRules;
  isActive: boolean;
  createdAt?: string;
  modifiedAt?: string;
}

export interface SaveAudienceRequest {
  name: string;
  rules: AudienceRules;
  isActive: boolean;
}

export interface AudienceRecipientPreview {
  email: string;
  name?: string;
  sourceType?: 'Lead' | 'Contact';
  leadId?: number;
  contactId?: number;
}

export interface AudiencePreviewResponse {
  totalSubscribedRecipients: number;
  totalSuppressedRecipients: number;
  sampleRecipients: AudienceRecipientPreview[];
}

export interface EmailConsent {
  emailConsentId: number;
  email: string;
  status: ConsentStatus;
  source: ConsentSource;
  changedAt: string;
}

export interface SaveConsentRequest {
  email: string;
  status: ConsentStatus;
  source: ConsentSource;
}

export enum CampaignChannel {
  Email = 'Email',
  Sms = 'Sms',
  WhatsApp = 'WhatsApp'
}

export enum CampaignStatus {
  Draft = 'Draft',
  Scheduled = 'Scheduled',
  Sending = 'Sending',
  Sent = 'Sent',
  Failed = 'Failed',
  Cancelled = 'Cancelled'
}

export enum RecipientStatus {
  Pending = 'Pending',
  Sent = 'Sent',
  Failed = 'Failed',
  Suppressed = 'Suppressed',
  Delivered = 'Delivered',
  Bounced = 'Bounced'
}

export interface Campaign {
  campaignId: number;
  name: string;
  channel: CampaignChannel;
  status: CampaignStatus;
  emailTemplateId?: number;
  audienceId?: number;
  scheduledAtUtc?: string;
  sentAtUtc?: string;
  totalRecipients: number;
  sentCount: number;
  failedCount: number;
  suppressedCount: number;
  openedCount: number;
  clickedCount: number;
  unsubscribedCount: number;
  createdAt?: string;
  modifiedAt?: string;
}

export interface SaveCampaignRequest {
  name: string;
  channel?: CampaignChannel;
  emailTemplateId?: number;
  audienceId?: number;
  scheduledAtUtc?: string;
}

export interface ScheduleCampaignRequest {
  scheduledAtUtc: string;
}

export interface CampaignRecipient {
  campaignRecipientId: number;
  campaignId: number;
  email: string;
  name?: string;
  leadId?: number;
  contactId?: number;
  status: RecipientStatus;
  sentAtUtc?: string;
  openedAtUtc?: string;
  clickedAtUtc?: string;
  errorMessage?: string;
}

export interface CampaignLink {
  campaignLinkId: number;
  campaignId: number;
  originalUrl: string;
  trackingToken: string;
  clickCount: number;
  createdAt?: string;
}

