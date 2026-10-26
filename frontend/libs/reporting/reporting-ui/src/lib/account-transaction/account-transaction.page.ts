import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AccountTransactionAccount,
  AccountTransactionResult,
  AccountTransactionService,
  DEFAULT_TRANSACTION_COLUMNS,
  JournalSourceOption,
  ReportColumn,
  TRANSACTION_COLUMNS,
  TaxRateOption,
  TransactionGridRow,
  appendTransactionPage,
  hasMoreTransactions,
  sideOf,
  sourceName,
  transactionRows,
  transactionSentence,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnChooserDialog,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'source' | 'contactId' | 'taxGroupId' | 'accountIds' | 'addAccount';

/** One account's card: the account, its grid rows and the columns its grid shows. */
interface AccountBlock {
  account: AccountTransactionAccount;
  rows: TransactionGridRow[];
  columns: ColumnDef[];
}

/**
 * The Account Transaction report (TK-218): its own page on its own endpoint. Every
 * posting in a period, account by account, with its contact, currency, tax rate
 * and a running balance on the account's normal side: the detailed general ledger
 * across many accounts at once.
 *
 * It opens on the financial year to date and runs straight away. Both dates are
 * required, so a missing or out-of-order one shows at its field; a refusal or a
 * failure shows in the message box. Accounts are added to the report one at a time
 * and removed again; with none, every account that has a posting is shown. The
 * postings come a page at a time, and the totals are of every posting the filters
 * match. Which columns a posting shows is the reader's choice.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-account-transaction-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
    ColumnChooserDialog,
  ],
  templateUrl: './account-transaction.page.html',
  styleUrl: './account-transaction.page.scss',
})
export class AccountTransactionPage implements OnInit {
  private readonly report = inject(AccountTransactionService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each posting is a small card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    source: new FormControl<string | null>(null),
    contactId: new FormControl<number | null>(null),
    taxGroupId: new FormControl<number | null>(null),
    /** Not part of the request: choosing an account here adds it to the list below and clears this. */
    addAccount: new FormControl<number | null>(null),
  });

  protected readonly sources = signal<JournalSourceOption[]>([]);
  protected readonly allAccounts = signal<BbSelectOption<number>[]>([]);
  protected readonly contactOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly taxRates = signal<TaxRateOption[]>([]);
  protected readonly choicesLoaded = signal(false);

  /** The accounts the report is narrowed to; empty is every account with a posting. */
  protected readonly chosenAccounts = signal<number[]>([]);

  protected readonly visibleColumns = signal<string[]>([...DEFAULT_TRANSACTION_COLUMNS]);
  protected readonly choosingColumns = signal(false);

  protected readonly result = signal<AccountTransactionResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly sourceOptions = computed<BbSelectOption<string>[]>(() =>
    this.sources().map((s) => ({ value: s.code, label: s.name })),
  );

  protected readonly taxOptions = computed<BbSelectOption<number>[]>(() =>
    this.taxRates().map((t) => ({ value: t.taxGroupId, label: t.label })),
  );

  /** The accounts not yet chosen, for the select that adds one. */
  protected readonly addableAccounts = computed<BbSelectOption<number>[]>(() => {
    const chosen = new Set(this.chosenAccounts());
    return this.allAccounts().filter((a) => !chosen.has(a.value));
  });

  /** The chosen accounts, named, for the chips. */
  protected readonly chosenChips = computed(() => {
    const names = new Map(this.allAccounts().map((a) => [a.value, a.label]));
    return this.chosenAccounts().map((id) => ({ id, label: names.get(id) ?? `Account ${id}` }));
  });

  /** Every column a posting can show, in the shape the shared column chooser takes. */
  protected readonly availableColumns: ReportColumn[] = TRANSACTION_COLUMNS.map((c) => ({
    key: c.key,
    header: c.header,
    dataType: 'Text',
    alignment: 'Left',
    width: null,
    isFilterable: false,
    isSortable: false,
    isGroupable: false,
    isPivotable: false,
    aggregate: 'None',
    isPrimary: false,
  }));

  /** Each account with its grid rows and its columns; the balance is headed with the side it is on. */
  protected readonly blocks = computed<AccountBlock[]>(() => {
    const result = this.result();
    if (!result) {
      return [];
    }

    return result.accounts.map((account) => ({
      account,
      rows: transactionRows(account, (value) => this.formats.formatAmount(value)),
      columns: this.columnsFor(account),
    }));
  });

  protected readonly moreToLoad = computed(() => {
    const result = this.result();
    return result ? hasMoreTransactions(result) : false;
  });

  /** The totals said in words, as a message; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalPostings > 0) {
      messages.push({ tone: 'info', text: transactionSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: TransactionGridRow): string =>
    row.kind === 'opening' ? 'row--subtotal' : row.kind === 'closing' ? 'row--total' : '';

  protected readonly sideOf = sideOf;

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
      const control: AbstractControl = this.filters.controls[name as keyof typeof this.filters.controls];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    this.filters.controls.addAccount.valueChanges.subscribe((id) => {
      if (id !== null) {
        this.chosenAccounts.update((chosen) => (chosen.includes(id) ? chosen : [...chosen, id]));
        this.filters.controls.addAccount.reset(null, { emitEvent: false });
        this.clearServerError('accountIds');
      }
    });

    void this.loadChoices();
    void this.run();
  }

  protected removeAccount(id: number): void {
    this.chosenAccounts.update((chosen) => chosen.filter((c) => c !== id));
  }

  protected clearAccounts(): void {
    this.chosenAccounts.set([]);
  }

  protected onColumnsChange(keys: string[]): void {
    this.visibleColumns.set(keys);
  }

  /** The words under a field: the server's refusal, else a missing required date once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name as keyof typeof this.filters.controls];
    if (!control || !control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : '';
  }

  protected accountError(): string {
    return this.serverErrors()['accountIds'] ?? '';
  }

  protected sourceLabel(code: string): string {
    return sourceName(code, this.sources());
  }

  /** Runs from the first page for the filters now chosen. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      this.result.set(await this.report.run(this.request(1)));
    } catch (err: unknown) {
      // Postings from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of postings under those already shown, for the filters they were run with. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreTransactions(loaded)) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);

    try {
      // The period the loaded pages were run for, not any edit made since.
      const next = await this.report.run({ ...this.request(loaded.pageNumber + 1), fromDate: loaded.fromDate, toDate: loaded.toDate });
      this.result.set(appendTransactionPage(loaded, next));
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private request(pageNumber: number) {
    const value = this.filters.getRawValue();
    return {
      fromDate: value.fromDate as string,
      toDate: value.toDate as string,
      accountIds: this.chosenAccounts(),
      contactId: value.contactId,
      taxGroupId: value.taxGroupId,
      source: value.source,
      pageNumber,
    };
  }

  /** The columns chosen, in the order chosen; the balance is headed with the account's side. */
  private columnsFor(account: AccountTransactionAccount): ColumnDef[] {
    const money = '7rem';
    const defs: Record<string, ColumnDef> = {
      date: { field: 'date', header: 'Date', width: '7rem' },
      documentNo: { field: 'documentNo', header: 'Document', width: '10rem' },
      source: { field: 'source', header: 'Source', width: '8rem' },
      relatedAccount: { field: 'relatedAccount', header: 'Related account', width: '10rem' },
      contact: { field: 'contact', header: 'Contact', width: '11rem' },
      description: { field: 'description', header: 'Description' },
      reference: { field: 'reference', header: 'Reference', width: '8rem' },
      tax: { field: 'tax', header: 'Tax rate', width: '8rem' },
      permitNo: { field: 'permitNo', header: 'Permit no.', width: '8rem' },
      foreign: { field: 'foreign', header: 'Foreign amount', width: '10rem' },
      debit: { field: 'debit', header: 'Debit', numeric: true, width: money },
      credit: { field: 'credit', header: 'Credit', numeric: true, width: money },
      balance: { field: 'balance', header: `Balance (${sideOf(account.normalSide)})`, numeric: true, width: money },
    };

    return this.visibleColumns()
      .map((key) => defs[key])
      .filter((column): column is ColumnDef => column !== undefined);
  }

  private async loadChoices(): Promise<void> {
    const [sources, accounts, contacts, taxes] = await Promise.all([
      this.report.sources().catch(() => [] as JournalSourceOption[]),
      this.report.accounts().catch(() => [] as BbSelectOption<number>[]),
      this.report.contacts().catch(() => [] as BbSelectOption<number>[]),
      this.report.taxRates().catch(() => [] as TaxRateOption[]),
    ]);
    this.sources.set(sources);
    this.allAccounts.set(accounts);
    this.contactOptions.set(contacts);
    this.taxRates.set(taxes);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Account Transaction report on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls || name === 'accountIds') {
        if (words?.[0]) {
          fieldErrors[name] = words[0];
        }
      }
    }

    if (Object.keys(fieldErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The Account Transaction report could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
