import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, FormRecord, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  ForeignCurrencyChoices,
  ForeignCurrencyGainLossResult,
  ForeignCurrencyGainLossService,
  ForeignCurrencyGridRow,
  currencyChoiceLabel,
  foreignCurrencyRows,
  foreignCurrencySentence,
  missingRatesSentence,
  rateKeyCurrency,
  ratesOf,
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
  registerDate,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'asAtDate' | 'currencyCode';

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * The Foreign Currency Gain or Loss report (TK-220): its own page on its own endpoint.
 * The exchange gains and losses on the branch's foreign-currency balances: realized by
 * the settlements of a period, unrealized on what is still open, worked out at a rate
 * for each currency that the person running it can change.
 *
 * It opens on the financial year to date, as at today, and runs straight away. Each rate
 * starts from the rate master's rate for the as-at date and is a field of its own; a
 * currency with no rate is named, and the report says so rather than showing a nought.
 * Both dates are required, so a missing or out-of-order one shows at its field, as does a
 * refused rate; a refusal or a failure shows in the message box. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-foreign-currency-gain-or-loss-page',
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
  templateUrl: './foreign-currency-gain-or-loss.page.html',
  styleUrl: './foreign-currency-gain-or-loss.page.scss',
})
export class ForeignCurrencyGainLossPage implements OnInit {
  private readonly report = inject(ForeignCurrencyGainLossService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each currency is a card rather than a block of rows. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    asAtDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    currencyCode: new FormControl<string | null>(null),
  });

  /** One revaluation rate for each currency with a balance at the as-at date, by currency code. */
  protected readonly rates = new FormRecord<FormControl<number | null>>({});

  protected readonly choices = signal<ForeignCurrencyChoices>({ baseCurrencyCode: '', currencies: [] });
  protected readonly choicesLoaded = signal(false);

  /** Where each currency's starting rate came from, for the hint under its field. */
  private readonly rateSources = signal<Record<string, string>>({});

  protected readonly result = signal<ForeignCurrencyGainLossResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});
  private readonly rateErrors = signal<Record<string, string>>({});

  /** Bumped by each look at the choices, so a slow answer for an earlier date is dropped. */
  private choiceRun = 0;

  protected readonly baseCurrency = computed(() => this.choices().baseCurrencyCode);
  protected readonly rateCurrencies = computed(() => this.choices().currencies.map((c) => c.currencyCode));

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.choices().currencies.map((c) => ({ value: c.currencyCode, label: currencyChoiceLabel(c) })),
  );

  protected readonly rows = computed<ForeignCurrencyGridRow[]>(() => {
    const result = this.result();
    return result ? foreignCurrencyRows(result) : [];
  });

  /** The cards of the phone layout: each currency's rows together, with the closing rows after them. */
  protected readonly groups = computed<ForeignCurrencyGridRow[][]>(() => {
    const groups: ForeignCurrencyGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'heading' || groups.length === 0 || row.kind === 'unassigned' || row.kind === 'total') {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'account', header: 'Account' },
    { field: 'dueSource', header: 'Due in currency', numeric: true, width: '8rem' },
    { field: 'carryingBase', header: 'At booked rates', numeric: true, width: '7.5rem' },
    { field: 'revaluedBase', header: 'At revaluation rate', numeric: true, width: '8rem' },
    { field: 'unrealized', header: 'Unrealized', numeric: true, width: '6.5rem' },
    { field: 'realized', header: 'Realized', numeric: true, width: '6.5rem' },
    { field: 'net', header: 'Net', numeric: true, width: '6.5rem' },
  ];

  /** The period's totals said in words, then currencies that have no rate; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.currencies.length + (result.unassignedRealized === 0 ? 0 : 1) > 0) {
      messages.push({ tone: 'info', text: foreignCurrencySentence(result, (value) => this.formats.formatMoney(value)) });
    }

    const missing = result ? missingRatesSentence(result) : '';
    if (missing) {
      messages.push({ tone: 'warning', text: missing });
    }

    return messages;
  });

  protected readonly rowClass = (row: ForeignCurrencyGridRow): string =>
    row.kind === 'heading' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

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

    // The rates and the currencies on offer belong to the as-at date.
    this.filters.controls.asAtDate.valueChanges.subscribe(() => void this.refreshChoices());

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
    return this.rates.controls[code];
  }

  protected rateHint(code: string): string {
    return this.rateSources()[code] ?? '';
  }

  protected rateError(code: string): string {
    return this.rateErrors()[code] ?? '';
  }

  /** Runs for the period, date, currency and rates now chosen. */
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
          asAtDate: value.asAtDate as string,
          currencyCode: value.currencyCode,
          rates: ratesOf(this.rates.getRawValue()),
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
   * Looks up the currencies with a balance at the as-at date, gives each a rate field, and
   * starts each from the rate master's rate for that date. A rate the person has already
   * typed is kept; a currency that no longer has a balance loses its field.
   */
  private async refreshChoices(): Promise<void> {
    const asAt = this.filters.controls.asAtDate.value;
    if (!asAt || !ISO_DATE.test(asAt)) {
      return;
    }

    const run = ++this.choiceRun;

    try {
      const choices = await this.report.choices(asAt);
      if (run !== this.choiceRun) {
        return;
      }

      this.choices.set(choices);
      this.syncRateControls(choices.currencies.map((c) => c.currencyCode));
      await this.startFromMasterRates(choices, asAt, run);
    } catch {
      if (run === this.choiceRun) {
        this.choices.set({ baseCurrencyCode: '', currencies: [] });
        this.syncRateControls([]);
      }
    } finally {
      if (run === this.choiceRun) {
        this.choicesLoaded.set(true);
      }
    }
  }

  private syncRateControls(codes: string[]): void {
    for (const code of Object.keys(this.rates.controls)) {
      if (!codes.includes(code)) {
        this.rates.removeControl(code);
      }
    }

    for (const code of codes) {
      if (!this.rates.contains(code)) {
        this.rates.addControl(code, new FormControl<number | null>(null));
      }
    }
  }

  private async startFromMasterRates(choices: ForeignCurrencyChoices, asAt: string, run: number): Promise<void> {
    const found = await Promise.all(
      choices.currencies.map(async (c) => ({
        code: c.currencyCode,
        rate: await this.report.masterRate(c.currencyCode, choices.baseCurrencyCode, asAt).catch(() => null),
      })),
    );

    if (run !== this.choiceRun) {
      return;
    }

    const sources: Record<string, string> = {};
    for (const { code, rate } of found) {
      const control = this.rates.controls[code];
      if (!control) {
        continue;
      }

      if (control.dirty) {
        sources[code] = 'The rate you entered.';
      } else if (rate !== null) {
        control.setValue(rate, { emitEvent: false });
        sources[code] = `The rate master's rate for ${registerDate(asAt)}.`;
      } else {
        control.setValue(null, { emitEvent: false });
        sources[code] = `No rate on file for ${registerDate(asAt)}: enter one.`;
      }
    }

    this.rateSources.set(sources);
  }

  /** A field's refusal goes to that field, a rate's to its currency; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Foreign Currency Gain or Loss report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Foreign Currency Gain or Loss report could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
