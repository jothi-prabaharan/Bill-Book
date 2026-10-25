import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  ForeignCurrencyDetailChoices,
  ForeignCurrencyDetailsResult,
  ForeignCurrencyGainLossDetailsService,
  ForeignCurrencyGainLossService,
  ForeignDetailGridRow,
  detailAccountLabel,
  detailCurrencyLabel,
  foreignCurrencySentence,
  foreignDetailRows,
  missingRatesSentence,
  rateKeyCurrency,
  rateRange,
  rateText,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  ExchangeRateInputComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';
import { FxRateFields } from '../foreign-currency-gain-or-loss/fx-rate-fields';

type FilterName = 'fromDate' | 'toDate' | 'currencyCode' | 'contactId' | 'accountId';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * The Foreign Currency Gain or Loss Details report (TK-221): its own page on its own endpoint.
 * The gains and losses of the Foreign Currency Gain or Loss report document by document: the
 * open documents that carry the unrealized amount and the settlements that posted the realized
 * one, under their currency and account, with the same totals as that report for the same dates
 * and rates.
 *
 * It opens on the financial year to date and runs straight away. Each rate starts from the rate
 * master's rate for the To date and can be changed; a currency with no rate is named and the
 * report says so rather than showing a nought. Both dates are required, so a missing or
 * out-of-order one shows at its field, as does a refused rate, contact or account; a refusal or
 * a failure shows in the message box. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-foreign-currency-gain-or-loss-details-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    ExchangeRateInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './foreign-currency-gain-or-loss-details.page.html',
  styleUrl: './foreign-currency-gain-or-loss-details.page.scss',
})
export class ForeignCurrencyGainLossDetailsPage implements OnInit {
  private readonly report = inject(ForeignCurrencyGainLossDetailsService);
  private readonly summary = inject(ForeignCurrencyGainLossService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each currency is a card rather than a block of rows. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    currencyCode: new FormControl<string | null>(null),
    contactId: new FormControl<number | null>(null),
    accountId: new FormControl<number | null>(null),
  });

  /** One revaluation rate for each currency with a balance at the To date, by currency code. */
  protected readonly rates = new FxRateFields((currency, base, on) => this.summary.masterRate(currency, base, on));

  protected readonly choices = signal<ForeignCurrencyDetailChoices>({ baseCurrencyCode: '', currencies: [], accounts: [], contacts: [] });
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<ForeignCurrencyDetailsResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});
  private readonly rateErrors = signal<Record<string, string>>({});

  /** Bumped by each look at the choices, so a slow answer for an earlier date is dropped. */
  private choiceRun = 0;

  protected readonly baseCurrency = computed(() => this.choices().baseCurrencyCode);

  /** The currencies a rate is asked for: those that still have a balance. */
  protected readonly rateCurrencies = computed(() => this.choices().currencies.filter((c) => c.hasBalance).map((c) => c.currencyCode));

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.choices().currencies.map((c) => ({ value: c.currencyCode, label: detailCurrencyLabel(c) })),
  );

  protected readonly contactOptions = computed<BbSelectOption<number>[]>(() =>
    this.choices().contacts.map((c) => ({ value: c.contactId, label: c.contactName })),
  );

  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.choices().accounts.map((a) => ({ value: a.accountId, label: detailAccountLabel(a) })),
  );

  protected readonly rows = computed<ForeignDetailGridRow[]>(() => {
    const result = this.result();
    return result ? foreignDetailRows(result) : [];
  });

  /** The cards of the phone layout: each currency's rows together, with the grand total on its own. */
  protected readonly groups = computed<ForeignDetailGridRow[][]>(() => {
    const groups: ForeignDetailGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'heading' || row.kind === 'total' || groups.length === 0) {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'date', header: 'Date', width: '6rem' },
    { field: 'document', header: 'Document' },
    { field: 'rates', header: 'Rate', numeric: true, width: '6rem' },
    { field: 'amount', header: 'Amount', numeric: true, width: '7.5rem' },
    { field: 'carryingBase', header: 'In base', numeric: true, width: '7.5rem' },
    { field: 'realized', header: 'Realized', numeric: true, width: '6rem' },
    { field: 'unrealized', header: 'Unrealized', numeric: true, width: '6rem' },
    { field: 'net', header: 'Net', numeric: true, width: '6rem' },
  ];

  /** The totals said in words, then currencies that have no rate; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.currencies.length + result.unassigned.length > 0) {
      messages.push({ tone: 'info', text: foreignCurrencySentence(result, (value) => this.formats.formatMoney(value)) });
    }

    const missing = result ? missingRatesSentence(result) : '';
    if (missing) {
      messages.push({ tone: 'warning', text: missing });
    }

    return messages;
  });

  protected readonly rowClass = (row: ForeignDetailGridRow): string =>
    row.kind === 'heading' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'currencyTotal' || row.kind === 'total' ? 'row--total' : '';

  /** A booked rate and the one it is now at, as the Rate column and the phone card show them. */
  protected readonly rateRange = (row: ForeignDetailGridRow): string => rateRange(row, rateText);

  /** Whether a row is a document or a settlement, which have a date, a number and rates of their own. */
  protected readonly isDocument = (row: ForeignDetailGridRow): boolean =>
    row.kind === 'open' || row.kind === 'settlement' || row.kind === 'unassigned';

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

    // The rates and the choices on offer belong to the To date.
    this.filters.controls.toDate.valueChanges.subscribe(() => void this.refreshChoices());

    void this.refreshChoices().then(() => this.run());
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

  protected rateControl(code: string): FormControl<number | null> {
    return this.rates.controls.controls[code];
  }

  protected rateHint(code: string): string {
    return this.rates.hint(code);
  }

  protected rateError(code: string): string {
    return this.rateErrors()[code] ?? '';
  }

  /** Runs for the period, filters and rates now chosen. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});
    this.rateErrors.set({});

    try {
      const value = this.filters.getRawValue();
      this.result.set(
        await this.report.run({
          fromDate: value.fromDate as string,
          toDate: value.toDate as string,
          currencyCode: value.currencyCode,
          contactId: value.contactId,
          accountId: value.accountId,
          rates: this.rates.values(),
        }),
      );
    } catch (err: unknown) {
      // Figures from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Looks up what the filters offer at the To date, gives each currency with a balance a rate
   * field, and starts each from the rate master's rate for that date. A rate the person has
   * already typed is kept; a currency that no longer has a balance loses its field.
   */
  private async refreshChoices(): Promise<void> {
    const toDate = this.filters.controls.toDate.value;
    if (!toDate || !ISO_DATE.test(toDate)) {
      return;
    }

    const run = ++this.choiceRun;

    try {
      const choices = await this.report.choices(toDate);
      if (run !== this.choiceRun) {
        return;
      }

      this.choices.set(choices);
      const codes = choices.currencies.filter((c) => c.hasBalance).map((c) => c.currencyCode);
      this.rates.sync(codes);
      await this.rates.startFromMaster(codes, choices.baseCurrencyCode, toDate, () => run === this.choiceRun);
    } catch {
      if (run === this.choiceRun) {
        this.choices.set({ baseCurrencyCode: '', currencies: [], accounts: [], contacts: [] });
        this.rates.sync([]);
      }
    } finally {
      if (run === this.choiceRun) {
        this.choicesLoaded.set(true);
      }
    }
  }

  /** A field's refusal goes to that field, a rate's to its currency; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Foreign Currency Gain or Loss Details report on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    const rateErrors: Record<string, string> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const currency = rateKeyCurrency(key);
      if (currency && words?.[0]) {
        rateErrors[currency] = words[0];
        continue;
      }

      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls && words?.[0]) {
        fieldErrors[name] = words[0];
      }
    }

    if (Object.keys(fieldErrors).length > 0 || Object.keys(rateErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      this.rateErrors.set(rateErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The Foreign Currency Gain or Loss Details report could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
