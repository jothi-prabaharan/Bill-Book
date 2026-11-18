import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  BankAccountDialogComponent,
  BankAccountRecord,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  IconButtonComponent,
  InfoTooltipComponent,
  MASTER_STATUSES,
  MessageBoxComponent,
  RegisterHeaderComponent,
  StatusTabsComponent,
  countStatusTabs,
  masterStatus,
  rowsInStatus,
} from '@bill-book/ui-components';

/** A row of the register: the account the popup edits, and what only the list shows. */
interface BankAccount extends BankAccountRecord {
  bankName: string | null;
  ledgerAccountId: number | null;
  ledgerAccountCode: string | null;
  isDefault: boolean;
  /** Money paid online through the client portal lands here (TK-98). */
  isOnlinePaymentAccount: boolean;
  displayOrder: number;
  isLedgerLinked: boolean;
}

/**
 * Banking › Bank accounts. Creating one creates its ledger account in
 * Accounting, because only Accounting writes ledger rows and an account with no
 * ledger identity cannot be posted to.
 *
 * The form is a popup (`bb-bank-account-dialog`, TK-268), the same one the
 * till opens from its Tender window, so this page keeps only the list and what
 * can be done to a row. When the ledger step fails the account still shows,
 * marked unlinked, and Link ledger retries it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-accounts-page',
  standalone: true,
  imports: [
    InfoTooltipComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    IconButtonComponent,
    MessageBoxComponent,
    RegisterHeaderComponent,
    StatusTabsComponent,
    BankAccountDialogComponent,
  ],
  host: { class: 'register-page' },
  templateUrl: './bank-accounts.page.html',
  styleUrl: './bank-accounts.page.scss',
})
export class BankAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly rows = signal<BankAccount[]>([]);
  /** The register's tabs (TK-190): every row is loaded, inactive ones too, and the tab filters. */
  protected readonly status = signal('Active');
  protected readonly statusTabs = computed(() => countStatusTabs(this.rows(), MASTER_STATUSES, masterStatus));
  protected readonly visibleRows = computed(() => rowsInStatus(this.rows(), this.status(), masterStatus));
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  /** Null while the popup is closed, 0 for a new account, otherwise the account being edited. */
  protected readonly editingId = signal<number | null>(null);
  protected readonly editingRow = computed(
    () => this.rows().find((row) => row.bankAccountId === this.editingId()) ?? null,
  );

  columns: ColumnDef[] = [
    { field: 'account', header: 'Account', isTemplate: true },
    { field: 'bank', header: 'Bank', isTemplate: true },
    { field: 'number', header: 'Number', isTemplate: true },
    { field: 'type', header: 'Type', isTemplate: true },
    { field: 'ifsc', header: 'IFSC', isTemplate: true },
    { field: 'ledger', header: 'Ledger', isTemplate: true },
    { field: 'default', header: 'Default', isTemplate: true },
    { field: 'online', header: 'Online payments', isTemplate: true },
    { field: 'active', header: 'Status', isTemplate: true },
    { field: 'actions', header: '', isTemplate: true },
  ];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.get<BankAccount[]>('/api/bank-accounts?includeInactive=true'),
      );
    } catch {
      this.fail('Could not load bank accounts.');
    } finally {
      this.busy.set(false);
    }
  }

  startAdd(): void {
    this.editingId.set(0);
  }

  startEdit(row: BankAccount): void {
    this.editingId.set(row.bankAccountId);
  }

  /** The popup saved the account: close it, say so, and show the list as it now is. */
  protected async onSaved(): Promise<void> {
    this.editingId.set(null);
    this.message.set('Bank account saved.');
    this.messageIsError.set(false);
    await this.load();
  }

  async linkLedger(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('POST', `/api/bank-accounts/${row.bankAccountId}/link-ledger`, {}),
      'Ledger account linked.',
    );
  }

  async makeDefault(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('PUT', `/api/bank-accounts/${row.bankAccountId}/default`, {}),
      'Default account changed.',
    );
  }

  /** Makes this the account the portal's online payments land in (TK-98). */
  async makeOnlinePayments(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('PUT', `/api/bank-accounts/${row.bankAccountId}/online-payments`, {}),
      'Online payments will land in this account.',
    );
  }

  async deactivate(row: BankAccount): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Deactivate Account',
      message: `Deactivate bank account "${row.accountName}"?`,
      confirmText: 'Deactivate',
      tone: 'danger',
    });
    if (!ok) return;

    await this.run(
      () => this.send('DELETE', `/api/bank-accounts/${row.bankAccountId}`, {}),
      'Bank account deactivated.',
    );
  }

  async onDrop(event: CdkDragDrop<BankAccount[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/bank-accounts/reorder', {
          movedId: moved.bankAccountId,
          previousId: list[event.currentIndex - 1]?.bankAccountId ?? null,
          nextId: list[event.currentIndex + 1]?.bankAccountId ?? null,
        }),
      null,
    );
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}

