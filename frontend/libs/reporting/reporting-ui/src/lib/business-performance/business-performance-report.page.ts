import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BUSINESS_PERFORMANCE_GROUPS,
  BusinessPerformanceRatio,
  BusinessPerformanceReportResult,
  BusinessPerformanceReportService,
  BusinessPerformanceUnit,
  previousPeriod,
  ratioChangeText,
  ratioValueText,
  samePeriodLastYear,
} from '@bill-book/reporting-core';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportDatePipe,
  UiMessage,
  rangeForPreset,
  registerAmount,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'compareFromDate' | 'compareToDate';

/**
 * Business Performance (TK-203): its own page on its own endpoint, at the path
 * the menu links to. The D-15 ratios for a period, and for a second period
 * beside it when asked: profitability, efficiency, financial position and
 * cash, each in its own unit with the figures it divides and the change.
 *
 * It opens on the financial year to date and runs straight away. A missing or
 * out-of-order date shows at its field; a refusal or a failure shows in the
 * message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-business-performance-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportDatePipe,
  ],
  templateUrl: './business-performance-report.page.html',
  styleUrl: './business-performance-report.page.scss',
})
export class BusinessPerformanceReportPage implements OnInit {
  private readonly report = inject(BusinessPerformanceReportService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where the figures take only the room they need and the ratio's name has the rest. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    compareFromDate: new FormControl<string | null>(null),
    compareToDate: new FormControl<string | null>(null),
  });

  protected readonly result = signal<BusinessPerformanceReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /** The ratios under each heading, in order. */
  protected readonly groups = computed(() => {
    const ratios = this.result()?.ratios ?? [];
    return BUSINESS_PERFORMANCE_GROUPS.map((g) => ({ ...g, ratios: ratios.filter((r) => r.group === g.group) }));
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = this.narrow() ? undefined : '9.5rem';
    const comparing = !!this.result()?.compareFromDate;
    return [
      { field: 'title', header: 'Ratio' },
      { field: 'value', header: 'This period', numeric: true, width },
      ...(comparing
        ? [
            { field: 'compareValue', header: 'Compared', numeric: true, width },
            { field: 'change', header: 'Change', numeric: true, width },
          ]
        : []),
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

  protected valueText(value: number | null, unit: BusinessPerformanceUnit): string {
    return ratioValueText(value, unit, (amount) => registerAmount(amount, this.formats.settings()));
  }

  protected changeText(change: number | null, unit: BusinessPerformanceUnit): string {
    return ratioChangeText(change, unit, (amount) => registerAmount(amount, this.formats.settings()));
  }

  /** The figures a ratio divides, as money: what to check it against on the Profit & Loss and the Balance Sheet. */
  protected figuresText(ratio: BusinessPerformanceRatio, days: number): string {
    if (ratio.unit === 'Amount' || ratio.numerator === null || ratio.denominator === null) {
      return '';
    }
    const money = (amount: number) => registerAmount(amount, this.formats.settings());
    const sum = `${money(ratio.numerator)} ÷ ${money(ratio.denominator)}`;
    return ratio.unit === 'Days' ? `${sum} × ${days} days` : ratio.unit === 'Percent' ? `${sum} × 100` : sum;
  }

  /** Fills the comparison with the period of the same length just before. */
  protected compareWithPreviousPeriod(): void {
    const { fromDate, toDate } = this.filters.getRawValue();
    if (fromDate && toDate) {
      const previous = previousPeriod(fromDate, toDate);
      this.filters.patchValue({ compareFromDate: previous.from, compareToDate: previous.to });
    }
  }

  /** Fills the comparison with the same days a year before. */
  protected compareWithLastYear(): void {
    const { fromDate, toDate } = this.filters.getRawValue();
    if (fromDate && toDate) {
      const last = samePeriodLastYear(fromDate, toDate);
      this.filters.patchValue({ compareFromDate: last.from, compareToDate: last.to });
    }
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
      this.failure.set('You do not have access to Business Performance on this branch.');
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

    this.failure.set(failure?.error?.message || 'Business Performance could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
