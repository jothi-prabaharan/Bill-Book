import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BankReconciliationAccount,
  BankSummaryGridRow,
  BankSummaryResult,
  BankSummaryService,
  bankAccountLabel,
  bankSummaryRows,
  bankSummarySentence,
  hasForeignAccounts,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'bankAccountId';

/**
 * The Bank Summary (TK-219): its own page on its own endpoint. For each bank and
 * cash account, what it held when the period began, what came in, what went out, any
 * revaluation of a foreign currency account, and what it holds at the end, in the
 * branch's base currency, with the account's own currency beside it for a foreign
 * one.
 *
 * It opens on the financial year to date and runs straight away. Both dates are
 * required, so a missing or out-of-order one shows at its field; a refusal or a
 * failure shows in the message box. The closing balance of each account is its
 * ledger balance at the end of the period, the Reconciliation Report's balance in
 * the books for that date.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-summary-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './bank-summary.page.html',
  styleUrl: './bank-summary.page.scss',
})
export class BankSummaryPage implements OnInit {
  private readonly report = inject(BankSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each account is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    bankAccountId: new FormControl<number | null>(null),
  });

  protected readonly accounts = signal<BankReconciliationAccount[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<BankSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.accounts().map((a) => ({ value: a.bankAccountId, label: bankAccountLabel(a) })),
  );

  protected readonly rows = computed<BankSummaryGridRow[]>(() => {
    const result = this.result();
    return result ? bankSummaryRows(result) : [];
  });

  protected readonly foreignNote = computed(() => {
    const result = this.result();
    return result ? hasForeignAccounts(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'account', header: 'Account' },
    { field: 'openingBalance', header: 'Opening', numeric: true, width: '8rem' },
    { field: 'received', header: 'Received', numeric: true, width: '8rem' },
    { field: 'spent', header: 'Spent', numeric: true, width: '8rem' },
    { field: 'revaluation', header: 'Revaluation', numeric: true, width: '7rem' },
    { field: 'closingBalance', header: 'Closing', numeric: true, width: '8rem' },
  ];

  /** The equation the closing balances satisfy, in words, as a message; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.accounts.length > 0) {
      messages.push({ tone: 'info', text: bankSummarySentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: BankSummaryGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
      const control: AbstractControl = this.filters.controls[name];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadChoices();
    void this.run();
  }

  /** The words under a field: the server's refusal, else a missing required date once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : '';
  }

  /** Runs for the period and bank account now chosen. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      const value = this.filters.getRawValue();
      this.result.set(
        await this.report.run({
          fromDate: value.fromDate as string,
          toDate: value.toDate as string,
          bankAccountId: value.bankAccountId,
        }),
      );
    } catch (err: unknown) {
      // Accounts from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadChoices(): Promise<void> {
    this.accounts.set(await this.report.accounts().catch(() => [] as BankReconciliationAccount[]));
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Bank Summary on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls && words?.[0]) {
        fieldErrors[name] = words[0];
      }
    }

    if (Object.keys(fieldErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The Bank Summary could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
