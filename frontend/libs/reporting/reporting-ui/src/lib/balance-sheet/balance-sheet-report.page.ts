import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BalanceSheetReportResult,
  BalanceSheetReportService,
  sameDayLastYear,
} from '@bill-book/reporting-core';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  UiMessage,
  registerAmount,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asOfDate' | 'compareAsOfDate' | 'isZeroBalanceShown';

/**
 * The Balance Sheet (TK-199): its own page on its own endpoint. What the
 * branch owns and owes at the end of a day, and at a second day beside it
 * when asked: assets, liabilities and equity, each account with its balance,
 * the year's profit and earlier years' carried into equity, and the totals
 * with a check that the two sides agree.
 *
 * It opens on today and runs straight away. A missing date shows at its field;
 * a refusal or a failure shows in the message box, and so does a sheet whose
 * two sides differ.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-balance-sheet-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './balance-sheet-report.page.html',
  styleUrl: './balance-sheet-report.page.scss',
})
export class BalanceSheetReportPage implements OnInit {
  private readonly report = inject(BalanceSheetReportService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where the amounts take only the room they need and the account name has the rest. */
  private readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    compareAsOfDate: new FormControl<string | null>(null),
    isZeroBalanceShown: new FormControl(false, { nonNullable: true }),
  });

  protected readonly result = signal<BalanceSheetReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /**
   * The account and its balance, and the comparison when the sheet has one.
   * Above phone width the amounts have a set width, so they line up from one
   * section to the next.
   */
  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = this.narrow() ? undefined : '9.5rem';
    return [
      { field: 'accountName', header: 'Account' },
      { field: 'balance', header: 'Balance', numeric: true, width },
      ...(this.result()?.compareAsOfDate ? [{ field: 'compareBalance', header: 'Compared', numeric: true, width }] : []),
    ];
  });

  /** The failure, else a warning when the two sides of the sheet differ. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }

    const sheet = this.result();
    if (!sheet || sheet.isBalanced) {
      return [];
    }

    const settings = this.formats.settings();
    const warnings: UiMessage[] = [];
    if (sheet.totals.difference !== 0) {
      warnings.push({
        tone: 'warning',
        text: `Assets and liabilities plus equity differ by ${registerAmount(sheet.totals.difference, settings)} on the as-at date. The ledger does not balance; check the journals posted up to that day.`,
      });
    }
    if (sheet.totals.compareDifference) {
      warnings.push({
        tone: 'warning',
        text: `On the comparison date they differ by ${registerAmount(sheet.totals.compareDifference, settings)}.`,
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

  /** Fills the comparison with the same day a year before the as-at date. */
  protected compareWithLastYear(): void {
    const asOf = this.filters.controls.asOfDate.value;
    if (asOf) {
      this.filters.controls.compareAsOfDate.setValue(sameDayLastYear(asOf));
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
          asOfDate: value.asOfDate as string,
          compareAsOfDate: value.compareAsOfDate,
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
      this.failure.set('You do not have access to the Balance Sheet on this branch.');
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

    this.failure.set(failure?.error?.message || 'The balance sheet could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
