import { HttpClient, HttpParams } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';
import {
  TrialBalanceReportRequest,
  TrialBalanceReportResult,
  TrialBalanceRow,
  samePeriodLastYear,
  trialBalanceRows,
  trialBalanceVerdict,
} from './trial-balance.models';

type FilterName = 'from' | 'to' | 'compareFrom' | 'compareTo' | 'isZeroBalanceShown';

/**
 * The Trial Balance (TK-215): every account's balance when the period opens,
 * what was posted to it and its balance when the period closes, grouped by
 * account type with a subtotal for each, and the totals at the foot.
 *
 * Both dates are required, so there is no "all dates": the page opens on the
 * financial year to date and runs straight away. Whether debits and credits
 * agree is said in words above the grid as well as shown by colour, and an
 * out-of-balance book is an error, never a tidy table with a discrepancy buried
 * in it. A missing or out-of-order date shows at its field; a refusal or a
 * failure shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-trial-balance-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './trial-balance.page.html',
  styleUrl: './trial-balance.page.scss',
})
export class TrialBalancePage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly filters = new FormGroup({
    from: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    to: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    compareFrom: new FormControl<string | null>(null),
    compareTo: new FormControl<string | null>(null),
    isZeroBalanceShown: new FormControl(false, { nonNullable: true }),
  });

  /** Phone width, where each account is a small card of its own rather than a row of eight figures. */
  protected readonly narrow = signal(false);

  protected readonly result = signal<TrialBalanceReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /** Whether the answer carries a second period, which adds two columns. */
  protected readonly comparing = computed(() => !!this.result()?.compareFrom);

  /** The grid's rows, worked out once per answer. */
  protected readonly rows = computed<TrialBalanceRow[]>(() => {
    const report = this.result();
    return report ? trialBalanceRows(report) : [];
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const width = '8.5rem';
    return [
      { field: 'accountName', header: 'Account', width: '16rem' },
      { field: 'openingDebit', header: 'Opening debit', numeric: true, width },
      { field: 'openingCredit', header: 'Opening credit', numeric: true, width },
      { field: 'movementDebit', header: 'Period debit', numeric: true, width },
      { field: 'movementCredit', header: 'Period credit', numeric: true, width },
      { field: 'closingDebit', header: 'Closing debit', numeric: true, width },
      { field: 'closingCredit', header: 'Closing credit', numeric: true, width },
      ...(this.comparing()
        ? [
            { field: 'compareDebit', header: 'Compared debit', numeric: true, width },
            { field: 'compareCredit', header: 'Compared credit', numeric: true, width },
          ]
        : []),
    ];
  });

  protected readonly rowClass = (row: TrialBalanceRow): string =>
    row.kind === 'group' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  /** The books' verdict, in words, as a message: success when they agree, an error when they do not. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const report = this.result();
    if (report && report.groups.length > 0) {
      const verdict = trialBalanceVerdict(report, (value) => this.formats.formatMoney(value));
      messages.push({ tone: verdict.isBalanced ? 'success' : 'error', text: verdict.text });
    }

    return messages;
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
      control.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.clearServerError(name));
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
    const { from, to } = this.filters.getRawValue();
    if (from && to) {
      const last = samePeriodLastYear(from, to);
      this.filters.patchValue({ compareFrom: last.from, compareTo: last.to });
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
        await this.fetch({
          from: value.from as string,
          to: value.to as string,
          compareFrom: value.compareFrom,
          compareTo: value.compareTo,
          isZeroBalanceShown: value.isZeroBalanceShown,
        }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private fetch(request: TrialBalanceReportRequest): Promise<TrialBalanceReportResult> {
    let params = new HttpParams().set('from', request.from).set('to', request.to);
    if (request.compareFrom) {
      params = params.set('compareFrom', request.compareFrom);
    }
    if (request.compareTo) {
      params = params.set('compareTo', request.compareTo);
    }
    if (request.isZeroBalanceShown) {
      params = params.set('isZeroBalanceShown', true);
    }

    return new Promise((resolve, reject) =>
      this.http.get<TrialBalanceReportResult>('/api/ledger/trial-balance', { params }).subscribe({
        next: resolve,
        error: reject,
      }),
    );
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the trial balance on this branch.');
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

    this.failure.set(failure?.error?.message || 'The trial balance could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
