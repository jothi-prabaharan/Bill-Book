import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CashFlowReportResult,
  CashFlowReportService,
  CashFlowRow,
  CashFlowSection,
  cashFlowRows,
  samePeriodLastYear,
} from '@bill-book/reporting-core';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  DataGridRowClass,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  UiMessage,
  rangeForPreset,
  registerAmount,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'compareFromDate' | 'compareToDate';

/**
 * The Cash Flow Statement – Direct (TK-201): its own page on its own endpoint,
 * at the path the menu links to. Where the branch's cash came from and went
 * over a period, and over a second period beside it when asked: operating,
 * investing and financing activities, each line with the accounts on the
 * other side of the movements, then the net change, the exchange-rate effect
 * and the cash at each end, with a check that they meet.
 *
 * It opens on the financial year to date and runs straight away. A missing or
 * out-of-order date shows at its field; a refusal or a failure shows in the
 * message box, and so does a statement whose two ends do not meet.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-cash-flow-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './cash-flow-report.page.html',
  styleUrl: './cash-flow-report.page.scss',
})
export class CashFlowReportPage implements OnInit {
  private readonly report = inject(CashFlowReportService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where the amounts take only the room they need and the label has the rest. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    compareFromDate: new FormControl<string | null>(null),
    compareToDate: new FormControl<string | null>(null),
  });

  protected readonly result = signal<CashFlowReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /** A line reads bold above its accounts. */
  protected readonly rowClass: DataGridRowClass = (row: CashFlowRow) => (row.kind === 'line' ? 'row--subtotal' : null);

  /**
   * The line or account and its cash, and the comparison when the statement
   * has one. Above phone width the amounts have a set width, so they line up
   * from one activity to the next and with the totals below.
   */
  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = this.narrow() ? undefined : '9.5rem';
    return [
      { field: 'label', header: 'Cash flow' },
      { field: 'amount', header: 'Amount', numeric: true, width },
      ...(this.result()?.compareFromDate ? [{ field: 'compareAmount', header: 'Compared', numeric: true, width }] : []),
    ];
  });

  /** The failure, else a warning when the cash at each end does not meet. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }

    const statement = this.result();
    if (!statement || statement.isReconciled) {
      return [];
    }

    const settings = this.formats.settings();
    const warnings: UiMessage[] = [];
    if (statement.totals.difference !== 0) {
      warnings.push({
        tone: 'warning',
        text: `Cash at the end differs from the flows by ${registerAmount(statement.totals.difference, settings)}. A posting in the period moved cash with nothing on the other side; check the journals that touch the cash and bank accounts.`,
      });
    }
    if (statement.totals.compareDifference) {
      warnings.push({
        tone: 'warning',
        text: `In the comparison period they differ by ${registerAmount(statement.totals.compareDifference, settings)}.`,
      });
    }
    return warnings;
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

  protected rowsOf(section: CashFlowSection): CashFlowRow[] {
    return cashFlowRows(section);
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
      this.failure.set('You do not have access to the Cash Flow Statement on this branch.');
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

    this.failure.set(failure?.error?.message || 'The cash flow statement could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
