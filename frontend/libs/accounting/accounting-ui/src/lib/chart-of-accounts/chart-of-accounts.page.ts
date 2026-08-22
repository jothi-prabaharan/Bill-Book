import { DataGridComponent, ColumnDef , TextInputComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface AccountRow {
  accountId: number;
  accountTypeId: number;
  accountCode: string;
  accountName: string;
  parentAccountId: number | null;
  currencyCode: string | null;
  isContra: boolean;
  isSystemDefault: boolean;
  isActive: boolean;
  isUsed: boolean;
  isLock: boolean;
  isJE: boolean;
  isSales: boolean;
  isPurchase: boolean;
  isPayment: boolean;
  isBank: boolean;
  isConfigLocked: boolean;
}

interface AccountType {
  accountTypeId: number;
  displayName: string;
  normalBalance: string;
  reportSection: string;
  sortOrder: number;
}

/**
 * Chart of accounts, grouped by account type. Once an account has been used its
 * type, code and usage flags are frozen — only the display name, active state
 * and posting lock stay editable. IsJE is never shown: it is backend-only.
 */
@Component({
  selector: 'bb-chart-of-accounts-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent],
  templateUrl: './chart-of-accounts.page.html',
  styleUrl: './chart-of-accounts.page.scss',
})
export class ChartOfAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly accounts = signal<AccountRow[]>([]);
  protected readonly types = signal<AccountType[]>([]);
  protected readonly editing = signal(false);
  protected readonly locked = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  showInactive = false;
  private editingId: number | null = null;

  form = {
    accountTypeId: 1,
    accountCode: '',
    accountName: '',
    parentAccountId: null as number | null,
    currencyCode: null as string | null,
    isContra: false,
    isActive: true,
    isLock: false,
    isSales: false,
    isPurchase: false,
    isPayment: false,
    isBank: false,
  };

  columns: ColumnDef[] = [
    { field: 'code', header: '', isTemplate: true },
    { field: 'name', header: '', isTemplate: true },
    { field: 'flags', header: '', isTemplate: true },
    { field: 'actions', header: '', isTemplate: true },
  ];

  /** Accounts grouped under their type, parents before their children. */
  protected readonly grouped = computed(() =>
    this.types()
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((type) => ({
        type,
        accounts: this.accounts()
          .filter((a) => a.accountTypeId === type.accountTypeId)
          .sort(
            (a, b) =>
              (a.parentAccountId ?? a.accountId) - (b.parentAccountId ?? b.accountId) ||
              a.accountCode.localeCompare(b.accountCode),
          ),
      })),
  );

  /** Only same-type accounts may parent, so the tree cannot cross report sections. */
  protected readonly parentOptions = computed(() =>
    this.accounts().filter(
      (a) => a.accountTypeId === this.form.accountTypeId && a.accountId !== this.editingId,
    ),
  );

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.types.set(await this.req<AccountType[]>('GET', '/api/master/account-types'));
      this.accounts.set(
        await this.req<AccountRow[]>('GET', `/api/accounts?includeInactive=${this.showInactive}`),
      );
    } catch {
      this.show('Could not load the chart of accounts.', true);
    } finally {
      this.busy.set(false);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.locked.set(false);
    this.form = {
      accountTypeId: this.types()[0]?.accountTypeId ?? 1,
      accountCode: '',
      accountName: '',
      parentAccountId: null,
      currencyCode: null,
      isContra: false,
      isActive: true,
      isLock: false,
      isSales: false,
      isPurchase: false,
      isPayment: false,
      isBank: false,
    };
    this.editing.set(true);
  }

  startEdit(account: AccountRow): void {
    this.editingId = account.accountId;
    this.locked.set(account.isConfigLocked);
    this.form = {
      accountTypeId: account.accountTypeId,
      accountCode: account.accountCode,
      accountName: account.accountName,
      parentAccountId: account.parentAccountId,
      currencyCode: account.currencyCode,
      isContra: account.isContra,
      isActive: account.isActive,
      isLock: account.isLock,
      isSales: account.isSales,
      isPurchase: account.isPurchase,
      isPayment: account.isPayment,
      isBank: account.isBank,
    };
    this.editing.set(true);
  }

  async save(): Promise<void> {
    if (
      this.form.accountTypeId <= 0 ||
      !this.hasText(this.form.accountCode) ||
      !this.hasText(this.form.accountName)
    ) {
      this.show('Type, code and name are required.', true);
      return;
    }

    this.busy.set(true);
    try {
      if (this.editingId === null) {
        await this.req('POST', '/api/accounts', this.form);
      } else {
        await this.req('PUT', `/api/accounts/${this.editingId}`, this.form);
      }
      this.editing.set(false);
      this.show('Account saved.', false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not save the account.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(account: AccountRow): Promise<void> {
    if (!confirm(`Deactivate "${account.accountName}"?`)) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/accounts/${account.accountId}`);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.show(anyErr?.error?.message ?? 'Could not deactivate the account.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
