import { ChangeDetectionStrategy } from '@angular/core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MASTER_STATUSES,
  MessageBoxComponent,
  RegisterHeaderComponent,
  SearchInputComponent,
  SelectComponent,
  StatusTabsComponent,
  countStatusTabs,
  masterStatus,
  rowsInStatus,
} from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface SubAccountRow {
  subAccountId: number;
  accountId: number;
  accountName: string;
  accountCode: string;
  accountTypeId: number;
  referenceType: string;
  referenceId: number;
  taxComponent: string;
  purpose: string;
  subAccountName: string;
  isActive: boolean;
}

/** Matches Accounting.Entity.Enums.SubAccountReferenceType, serialized as a string. */
const REFERENCE_TYPES = ['Contact', 'Item', 'Tax'] as const;
type ReferenceType = (typeof REFERENCE_TYPES)[number];

/**
 * The sub-ledger beneath the control accounts. Read-only by design: every row is
 * created as a side effect of the master that owns it — a contact, an item, a tax
 * rate — so there is nothing here for a user to add or edit. This page exists to
 * answer "what is posting under Accounts Receivable?" without a database query.
 *
 * A contact has three rows under each of receivables and payables — the trade
 * balance and two kinds of advance — so the purpose is shown beside the name.
 * The advances are the part of the control account that is not a trade balance,
 * which is exactly what a balance sheet has to report separately.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sub-accounts-page',
  standalone: true,
  imports: [
    DataGridCellTemplateDirective,
    DataGridComponent,
    FormsModule,
    MessageBoxComponent,
    RegisterHeaderComponent,
    SearchInputComponent,
    SelectComponent,
    StatusTabsComponent,
  ],
  host: { class: 'register-page' },
  templateUrl: './sub-accounts.page.html',
  styleUrl: './sub-accounts.page.scss',
})
export class SubAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly referenceTypes = REFERENCE_TYPES;
  protected readonly rows = signal<SubAccountRow[]>([]);

  columns: ColumnDef[] = [
    { field: 'control', header: 'Control account' },
    { field: 'subAccountName', header: 'Name' },
    { field: 'tags', header: 'Tags' },
    { field: 'referenceId', header: 'Reference' },
  ];

  protected readonly typeOptions: BbSelectOption<string>[] = [
    { value: '', label: 'Every owner' },
    ...REFERENCE_TYPES.map((type) => ({ value: type, label: type })),
  ];
  protected readonly referenceType = signal<ReferenceType | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  search = '';

  /**
   * One register (TK-190), ordered by the control account each row hangs from
   * and then by name, which is the order that answers what is posting under
   * Accounts Receivable. A retired contact's sub-accounts survive for history,
   * on the Inactive tab.
   */
  protected readonly status = signal('Active');
  private readonly matching = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    return this.rows()
      .filter((s) => term === '' || s.subAccountName.toLowerCase().includes(term))
      .sort((a, b) => a.accountCode.localeCompare(b.accountCode) || a.subAccountName.localeCompare(b.subAccountName));
  });
  protected readonly statusTabs = computed(() => countStatusTabs(this.matching(), MASTER_STATUSES, masterStatus));
  protected readonly visibleRows = computed(() => rowsInStatus(this.matching(), this.status(), masterStatus));

  ngOnInit(): void {
    void this.load();
  }

  setType(type: ReferenceType | null): void {
    this.referenceType.set(type);
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      const type = this.referenceType();
      const query = type === null ? '' : `?referenceType=${type}`;
      this.rows.set(await this.req<SubAccountRow[]>('GET', `/api/sub-accounts${query}`));
    } catch {
      this.message.set('Could not load sub-accounts.');
    } finally {
      this.busy.set(false);
    }
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}

