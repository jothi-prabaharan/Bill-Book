import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionContextService, normaliseBusinessType } from '@bill-book/auth';
import {
  BbSelectOption,
  ColumnDef,
  CustomFieldDefinition,
  CustomFieldValues,
  DataGridCellTemplateDirective,
  DataGridComponent,
  ExportImportComponent,
  MessageBoxComponent,
  SearchInputComponent,
  SelectComponent,
  customListColumns,
  withCustomColumns,
} from '@bill-book/ui-components';

interface ContactListItem {
  contactId: number;
  contactCode: string;
  displayName: string;
  contactCategory: string;
  isCustomer: boolean;
  isVendor: boolean;
  isJobWorker: boolean;
  isPrescriber: boolean;
  /** A student's guardian (School, TK-60). */
  isGuardian: boolean;
  gstin: string | null;
  gstRegistrationType: string;
  currencyCode: string;
  creditLimit: number | null;
  isActive: boolean;
  /** Saved to finish later (TK-188): on no document picker until saved. */
  isDraft: boolean;
  isSubLedgerLinked: boolean;
  email: string | null;
  mobileNumber: string | null;
  city: string | null;
  /** The contact's custom field values (TK-111). */
  customFields?: CustomFieldValues | null;
}

/**
 * Customers, vendors, job workers and prescribers: one master with role flags,
 * because in Indian SMB books the same party is routinely both a customer and a
 * vendor.
 *
 * The list only. A contact opens on its own page, `/contacts/new` and
 * `/contacts/:id` (TK-188), which is where it is edited and saved. Drafts are
 * listed here, marked, and nowhere else.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contacts-page',
  standalone: true,
  imports: [
    DataGridCellTemplateDirective,
    DataGridComponent,
    ExportImportComponent,
    FormsModule,
    MessageBoxComponent,
    SearchInputComponent,
    SelectComponent,
  ],
  templateUrl: './contacts.page.html',
  styleUrl: './contacts.page.scss',
})
export class ContactsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(SessionContextService);

  protected readonly rows = signal<ContactListItem[]>([]);

  /** The branch's custom fields on contacts (TK-111): list columns and search. */
  protected readonly customFieldDefs = signal<CustomFieldDefinition[]>([]);
  protected readonly gridColumns = computed<ColumnDef[]>(() => [...this.columns, ...customListColumns(this.customFieldDefs())]);
  protected readonly gridRows = computed(() => withCustomColumns(this.rows(), this.customFieldDefs()));
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  protected readonly statusFilterOptions: BbSelectOption<string>[] = [
    { value: 'active', label: 'Active only' },
    { value: 'any', label: 'Any status' },
  ];

  search = '';
  roleFilter = '';
  showInactive = false;

  columns: ColumnDef[] = [
    { field: 'contactCode', header: 'CONTACT CODE' },
    { field: 'nameGstin', header: 'NAME · GSTIN' },
    { field: 'city', header: 'CITY' },
    { field: 'role', header: 'ROLE' },
    { field: 'outstanding', header: 'OUTSTANDING', dataType: 'money' },
    { field: 'creditLimit', header: 'CREDIT LIMIT', dataType: 'money' },
  ];

  protected get statusFilter(): string {
    return this.showInactive ? 'any' : 'active';
  }

  protected set statusFilter(value: string) {
    this.showInactive = value === 'any';
  }

  /** Guardians are School's (TK-60): the filter shows only in a School branch. */
  protected isSchool(): boolean {
    return normaliseBusinessType(this.session.context()?.businessType) === 'School';
  }

  ngOnInit(): void {
    // `?action=create` is how the section menu and older links ask for a new
    // contact; the form has its own page now, so pass them on to it.
    const params = this.route.snapshot.queryParamMap;
    if (params.get('action') === 'create') {
      const queryParams: Record<string, string> = {};
      for (const key of ['role', 'returnUrl', 'returnMaster']) {
        const value = params.get(key);
        if (value) queryParams[key] = value;
      }
      void this.router.navigate(['/contacts/new'], { queryParams, replaceUrl: true });
      return;
    }

    void this.load();
    void this.loadCustomFields();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = new URLSearchParams();
      if (this.search.trim()) query.set('search', this.search.trim());
      if (this.roleFilter) query.set('role', this.roleFilter);
      query.set('includeInactive', String(this.showInactive));
      // This screen is the one place drafts are listed (TK-188).
      query.set('includeDrafts', 'true');

      this.rows.set(await this.get<ContactListItem[]>(`/api/contacts?${query}`));
      this.message.set(null);
    } catch {
      this.message.set('Could not load contacts.');
    } finally {
      this.busy.set(false);
    }
  }

  /** A new contact in the given role: `/contacts/new?role=vendor`. */
  openAdd(role: 'customer' | 'vendor' = 'customer'): void {
    void this.router.navigate(['/contacts/new'], { queryParams: { role } });
  }

  openEdit(row: ContactListItem): void {
    void this.router.navigate(['/contacts', row.contactId]);
  }

  roleLabels(row: ContactListItem): string {
    const roles: string[] = [];
    if (row.isCustomer) roles.push('Customer');
    if (row.isVendor) roles.push('Vendor');
    if (row.isJobWorker) roles.push('Job worker');
    if (row.isPrescriber) roles.push('Prescriber');
    if (row.isGuardian) roles.push('Guardian');
    return roles.join(' & ') || '—';
  }

  private async loadCustomFields(): Promise<void> {
    try {
      this.customFieldDefs.set(await this.get<CustomFieldDefinition[]>('/api/contacts/custom-fields'));
    } catch {
      /* a branch with no custom fields: the list works without them */
    }
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) => this.http.get<T>(url).subscribe({ next: resolve, error: reject }));
  }
}
