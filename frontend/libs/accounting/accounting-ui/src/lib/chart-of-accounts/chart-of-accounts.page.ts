import { ExportImportComponent } from '@bill-book/ui-components';
import { ChangeDetectionStrategy } from '@angular/core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MoneyCellComponent,
  PeriodFilterBarComponent,
  PeriodRange,
  RegisterHeaderComponent,
  RegisterStatusOption,
  SelectComponent,
  StatusTabsComponent,
  TextInputComponent,
  countStatusTabs,
  rangeForPreset,
} from '@bill-book/ui-components';
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

/** One account's net position over the period, from the trial balance. */
interface TrialBalanceRow {
  accountId: number;
  debitBalance: number;
  creditBalance: number;
}

export type AccountState = 'Active' | 'Inactive' | 'Locked';

/** An account as the register draws it: its type's name, its state and its balance for the period. */
export interface AccountRegisterRow extends AccountRow {
  accountTypeName: string;
  state: AccountState;
  debitBalance: number;
  creditBalance: number;
}

/**
 * Which tab an account falls under (TK-187). Inactive first: an inactive
 * account cannot be posted to whether or not it is also locked.
 */
export function accountState(account: Pick<AccountRow, 'isActive' | 'isLock'>): AccountState {
  if (!account.isActive) return 'Inactive';
  return account.isLock ? 'Locked' : 'Active';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-chart-of-accounts-page',
  standalone: true,
  imports: [ExportImportComponent, 
    DataGridComponent,
    DataGridCellTemplateDirective,
    RegisterHeaderComponent,
    StatusTabsComponent,
    PeriodFilterBarComponent,
    MoneyCellComponent,
    FormsModule,
    TextInputComponent,
    CheckboxComponent,
    SelectComponent,
  ],
  host: { class: 'register-page' },
  templateUrl: './chart-of-accounts.page.html',
  styleUrl: './chart-of-accounts.page.scss',
})
export class ChartOfAccountsPage implements OnInit {

  protected readonly accountTypeOptions = computed<BbSelectOption<number>[]>(() =>
    this.types().map((type) => ({ value: type.accountTypeId, label: type.displayName })),
  );

  protected readonly parentAccountOptions = computed<BbSelectOption<number>[]>(() =>
    this.parentOptions().map((account) => ({
      value: account.accountId,
      label: `${account.accountCode} · ${account.accountName}`,
    })),
  );
  private readonly http = inject(HttpClient);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly accounts = signal<AccountRow[]>([]);
  protected readonly types = signal<AccountType[]>([]);
  protected readonly editing = signal(false);
  protected readonly locked = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  /** Each account's debit or credit balance over the period, by account id. */
  protected readonly balances = signal<ReadonlyMap<number, TrialBalanceRow>>(new Map());

  /** The period the balances cover; a dated register opens on the financial year. */
  protected readonly range = signal<PeriodRange>(rangeForPreset('this-financial-year'));

  /** The state tab chosen; empty is All. */
  protected readonly stateFilter = signal('');

  protected readonly states: readonly RegisterStatusOption[] = [
    { value: 'Active', label: 'Active' },
    { value: 'Inactive', label: 'Inactive' },
    { value: 'Locked', label: 'Locked' },
  ];

  /** The account being edited, for the editor's Deactivate. */
  protected readonly editingAccount = signal<AccountRow | null>(null);

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
    { field: 'accountCode', header: 'Account code', isTemplate: true },
    { field: 'accountTypeName', header: 'Account type', isTemplate: true },
    { field: 'accountName', header: 'Account name', isTemplate: true },
    { field: 'state', header: 'State', isTemplate: true },
    { field: 'debitBalance', header: 'Debit', dataType: 'money', isTemplate: true },
    { field: 'creditBalance', header: 'Credit', dataType: 'money', isTemplate: true },
  ];

  /**
   * One register of every account (TK-187), in the order the chart reads: by
   * type, then each parent followed by its children, then by code.
   */
  protected readonly registerRows = computed<AccountRegisterRow[]>(() => {
    const types = new Map(this.types().map((t) => [t.accountTypeId, t]));
    const balances = this.balances();
    return this.accounts()
      .slice()
      .sort(
        (a, b) =>
          (types.get(a.accountTypeId)?.sortOrder ?? 0) - (types.get(b.accountTypeId)?.sortOrder ?? 0) ||
          (a.parentAccountId ?? a.accountId) - (b.parentAccountId ?? b.accountId) ||
          a.accountCode.localeCompare(b.accountCode),
      )
      .map((account) => ({
        ...account,
        accountTypeName: types.get(account.accountTypeId)?.displayName ?? '',
        state: accountState(account),
        debitBalance: balances.get(account.accountId)?.debitBalance ?? 0,
        creditBalance: balances.get(account.accountId)?.creditBalance ?? 0,
      }));
  });

  protected readonly tabs = computed(() => countStatusTabs(this.registerRows(), this.states, (row) => row.state));

  protected readonly visibleRows = computed(() => {
    const state = this.stateFilter();
    return this.registerRows().filter((row) => !state || row.state === state);
  });

  /**
   * The credit side of the accounts on screen. Across the whole chart it
   * equals the debit side, which is what a balanced set of books means.
   */
  protected readonly periodTotal = computed(() =>
    this.visibleRows().reduce((sum, row) => sum + row.creditBalance, 0),
  );

  protected stateTone(state: AccountState): string {
    switch (state) {
      case 'Active':
        return 'tag-neutral';
      case 'Locked':
        return 'tag-outline';
      default:
        return 'tag-info';
    }
  }

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
      // Inactive accounts too: the Inactive tab counts them.
      const [types, accounts] = await Promise.all([
        this.req<AccountType[]>('GET', '/api/master/account-types'),
        this.req<AccountRow[]>('GET', '/api/accounts?includeInactive=true'),
      ]);
      this.types.set(types);
      this.accounts.set(accounts);
      await this.loadBalances();
    } catch {
      this.show('Could not load the chart of accounts.', true);
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Each account's balance for the period, from the trial balance: one column
   * per account, the side its net falls on. A user who may not read the ledger
   * still gets the chart, with no balances.
   */
  private async loadBalances(): Promise<void> {
    const { from, to } = this.range();
    const query = [from ? `from=${from}` : '', to ? `to=${to}` : ''].filter(Boolean).join('&');
    try {
      const view = await this.req<{ rows: TrialBalanceRow[] }>(
        'GET',
        `/api/ledger/trial-balance${query ? `?${query}` : ''}`,
      );
      this.balances.set(new Map(view.rows.map((row) => [row.accountId, row])));
    } catch {
      this.balances.set(new Map());
    }
  }

  protected async setRange(range: PeriodRange): Promise<void> {
    this.range.set(range);
    await this.loadBalances();
  }

  startCreate(): void {
    this.editingAccount.set(null);
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
    this.editingAccount.set(account);
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
    const ok = await this.confirmDialog.confirm({
      title: 'Deactivate Account',
      message: `Deactivate "${account.accountName}"?`,
      confirmText: 'Deactivate',
      tone: 'danger',
    });
    if (!ok) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/accounts/${account.accountId}`);
      this.editing.set(false);
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

