import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ProfitAndLossReportResult,
  ProfitAndLossReportService,
  percentText,
  samePeriodLastYear,
} from '@bill-book/reporting-core';
import {
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'compareFromDate' | 'compareToDate' | 'isZeroBalanceShown';

/**
 * The Profit & Loss (TK-200): its own page on its own endpoint. What the
 * branch earned and spent over a period, and over a second period beside it
 * when asked: revenue less cost of sales to gross profit, then other income
 * and expenses to net profit, each account with its movement and each profit
 * line with its share of revenue.
 *
 * It opens on the financial year to date and runs straight away. A missing or
 * out-of-order date shows at its field; a refusal or a failure shows in the
 * message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-profit-and-loss-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './profit-and-loss-report.page.html',
  styleUrl: './profit-and-loss-report.page.scss',
})
export class ProfitAndLossReportPage implements OnInit {
  private readonly report = inject(ProfitAndLossReportService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where the amounts take only the room they need and the account name has the rest. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    compareFromDate: new FormControl<string | null>(null),
    compareToDate: new FormControl<string | null>(null),
    isZeroBalanceShown: new FormControl(false, { nonNullable: true }),
  });

  protected readonly result = signal<ProfitAndLossReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly percent = percentText;

  /**
   * The account and its movement, and the comparison when the report has one.
   * Above phone width the amounts have a set width, so they line up from one
   * section to the next and with the profit lines between them.
   */
  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = this.narrow() ? undefined : '9.5rem';
    return [
      { field: 'accountName', header: 'Account' },
      { field: 'amount', header: 'Amount', numeric: true, width },
      ...(this.result()?.compareFromDate ? [{ field: 'compareAmount', header: 'Compared', numeric: true, width }] : []),
    ];
  });

  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    return failure ? [{ tone: 'error', text: failure }] : [];
  });

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

    return control.errors['required'] ? `${label} is required.` : `Check the ${label.toLowerCase()}.`;
  }

  /** Fills the comparison with the same days a year before the period. */
  protected compareWithLastYear(): void {
    const { fromDate, toDate } = this.filters.getRawValue();
    if (fromDate && toDate) {
      const last = samePeriodLastYear(fromDate, toDate);
      this.filters.patchValue({ compareFromDate: last.from, compareToDate: last.to });
    }
  }

  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    const value = this.filters.getRawValue();
    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      this.result.set(
        await this.report.run({
          fromDate: value.fromDate as string,
          toDate: value.toDate as string,
          compareFromDate: value.compareFromDate,
          compareToDate: value.compareToDate,
          isZeroBalanceShown: value.isZeroBalanceShown,
        }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Profit & Loss on this branch.');
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

    this.failure.set(failure?.error?.message || 'The profit and loss could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
