import { DataGridComponent, ColumnDef , SearchInputComponent } from '@bill-book/ui-components';
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
  selector: 'bb-sub-accounts-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, SearchInputComponent],
  templateUrl: './sub-accounts.page.html',
  styleUrl: './sub-accounts.page.scss',
})
export class SubAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly referenceTypes = REFERENCE_TYPES;
  protected readonly rows = signal<SubAccountRow[]>([]);

  columns: ColumnDef[] = [
    { field: 'subAccountName', header: 'Name' },
    { field: 'tags', header: 'Tags' },
    { field: 'referenceId', header: 'Reference' }
  ];
  protected readonly referenceType = signal<ReferenceType | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  showInactive = false;
  search = '';

  /**
   * Grouped under the control account they hang from, which is the only ordering
   * that answers the question this page is for. Inactive rows are hidden unless
   * asked for — a retired contact's sub-accounts survive for history.
   */
  protected readonly grouped = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const visible = this.rows().filter(
      (s) =>
        (this.showInactive || s.isActive) &&
        (term === '' || s.subAccountName.toLowerCase().includes(term)),
    );

    const groups = new Map<number, { accountId: number; accountCode: string; accountName: string; rows: SubAccountRow[] }>();
    for (const row of visible) {
      const group = groups.get(row.accountId);
      if (group) {
        group.rows.push(row);
      } else {
        groups.set(row.accountId, {
          accountId: row.accountId,
          accountCode: row.accountCode,
          accountName: row.accountName,
          rows: [row],
        });
      }
    }

    return [...groups.values()].sort((a, b) => a.accountCode.localeCompare(b.accountCode));
  });

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
