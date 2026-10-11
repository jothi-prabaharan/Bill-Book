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
  MoneyCellComponent,
  RegisterHeaderComponent,
  RegisterStatusOption,
  SearchInputComponent,
  SelectComponent,
  StatusTabsComponent,
  countStatusTabs,
  customListColumns,
  rowsInStatus,
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
 *
 * In the register design (TK-190): every contact is loaded, inactive and
 * draft ones included, and the status tabs filter them, so each tab's count is
 * right. Search and role still go to the server, which also searches the
 * branch's searchable custom fields.
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
    MoneyCellComponent,
    RegisterHeaderComponent,
    SearchInputComponent,
    SelectComponent,
    StatusTabsComponent,
  ],
  host: { class: 'register-page' },
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

  /** Active first: a list that opens on contacts nobody uses any more reads as clutter. */
  protected readonly status = signal('Active');
  private readonly statuses: readonly RegisterStatusOption[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Inactive', label: 'Inactive' },
  ];
  protected readonly statusTabs = computed(() => countStatusTabs(this.rows(), this.statuses, (row) => this.statusOf(row)));
  protected readonly gridRows = computed(() =>
    withCustomColumns(rowsInStatus(this.rows(), this.status(), (row) => this.statusOf(row)), this.customFieldDefs()),
  );

  /** Every role, with guardians only in a School branch (TK-60). */
  protected readonly roleOptions = computed<BbSelectOption<string>[]>(() => [
    { value: '', label: 'Every role' },
    { value: 'customer', label: 'Customers' },
    { value: 'vendor', label: 'Vendors' },
    { value: 'jobworker', label: 'Job workers' },
    { value: 'prescriber', label: 'Prescribers' },
    ...(this.isSchool() ? [{ value: 'guardian', label: 'Guardians' }] : []),
  ]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  search = '';
  roleFilter = '';

  columns: ColumnDef[] = [
    { field: 'contactCode', header: 'Code' },
    { field: 'nameGstin', header: 'Name · GSTIN' },
    { field: 'city', header: 'City' },
    { field: 'role', header: 'Role' },
    { field: 'status', header: 'Status' },
    { field: 'creditLimit', header: 'Credit limit', dataType: 'money', align: 'right' },
  ];

  protected statusOf(row: ContactListItem): string {
    return row.isDraft ? 'Draft' : row.isActive ? 'Active' : 'Inactive';
  }

  protected statusTone(row: ContactListItem): string {
    return row.isDraft ? 'tag-warning' : row.isActive ? 'tag-success' : 'tag-neutral';
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
      // Every status, filtered by the tabs; this screen is the one place
      // drafts are listed (TK-188).
      query.set('includeInactive', 'true');
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
