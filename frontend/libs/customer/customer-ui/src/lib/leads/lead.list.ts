import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
} from '@bill-book/ui-components';
import { CustomerService, Lead } from '@bill-book/customer-core';
import { LeadFormComponent } from './lead-form.component';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-lead-list',
  standalone: true,
  imports: [
    CommonModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    LeadFormComponent,
    LookupDialogComponent,
  ],
  templateUrl: './lead.list.html',
  styleUrl: './lead.list.scss'
})
export class LeadList implements OnInit {
  private readonly customerService = inject(CustomerService);

  readonly leads = signal<Lead[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);

  /** The lead being converted, if the choice is open. */
  readonly converting = signal<Lead | null>(null);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);

  /** The existing-contact picker, one step past the choice. */
  readonly pickerOpen = signal(false);
  readonly pickerRows = signal<LookupRow[]>([]);
  readonly pickerLoading = signal(false);

  readonly columns: ColumnDef[] = [
    { field: 'name', title: 'Name', dataType: 'string' },
    { field: 'companyName', title: 'Company', dataType: 'string' },
    { field: 'email', title: 'Email', dataType: 'string' },
    { field: 'phone', title: 'Phone', dataType: 'string' },
    { field: 'source', title: 'Source', dataType: 'string' },
    { field: 'status', title: 'Status', dataType: 'string' },
    { field: 'actions', title: 'Actions', isTemplate: true }
  ];

  ngOnInit() {
    void this.loadLeads();
  }

  async loadLeads() {
    this.loading.set(true);
    try {
      const data = await this.customerService.getLeads();
      this.leads.set(data);
    } catch (err) {
      console.error('Failed to load leads', err);
      this.message.set('Could not load leads.');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Starts a conversion by asking which of the two paths to take.
   *
   * The button used to convert straight away, which only worked because the
   * server would accept a conversion with no contact named. It no longer does —
   * a lead becomes either a brand-new contact or an existing one, and guessing
   * between them is how duplicate contacts get made.
   */
  startConvert(lead: Lead): void {
    this.message.set(null);
    this.converting.set(lead);
  }

  cancelConvert(): void {
    this.converting.set(null);
    this.pickerOpen.set(false);
  }

  /** Contacts makes the contact, from this lead's own name, phone and email. */
  async convertToNewContact(): Promise<void> {
    const lead = this.converting();

    if (lead === null) {
      return;
    }

    await this.convert(lead, undefined);
  }

  openContactPicker(): void {
    this.pickerOpen.set(true);
    void this.searchContacts('');
  }

  async searchContacts(term: string): Promise<void> {
    this.pickerLoading.set(true);

    try {
      const contacts = await this.customerService.searchContacts(term);

      this.pickerRows.set(
        contacts.map((contact) => ({
          id: contact.contactId,
          code: contact.contactCode,
          name: contact.displayName,
          meta: contact.gstin,
        })),
      );
    } catch (err) {
      console.error('Failed to search contacts', err);
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  async chooseContact(row: LookupRow): Promise<void> {
    const lead = this.converting();

    this.pickerOpen.set(false);

    if (lead !== null) {
      await this.convert(lead, row.id);
    }
  }

  private async convert(lead: Lead, contactId: number | undefined): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      await this.customerService.convertLead(lead.leadId, contactId);
      this.converting.set(null);
      await this.loadLeads();
    } catch (err: unknown) {
      // The server's message is worth showing verbatim: it says whether the
      // lead had no way to be reached, whether the contact belongs to another
      // branch, or whether this user may not create contacts — three different
      // next steps, and none of them guessable from "conversion failed".
      this.message.set(this.serverMessage(err) ?? 'Could not convert this lead.');
    } finally {
      this.busy.set(false);
    }
  }

  private serverMessage(error: unknown): string | null {
    const body = (error as { error?: { message?: unknown } } | null)?.error;
    const message = body?.message;

    if (typeof message === 'string') {
      return message;
    }

    return (error as { status?: number } | null)?.status === 403
      ? 'That contact belongs to another branch, or you may not create contacts.'
      : null;
  }

  onSaved() {
    this.showForm.set(false);
    void this.loadLeads();
  }
}
