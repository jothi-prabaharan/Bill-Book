import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BankReconciliationAccount,
  BankReconciliationReportResult,
  BankReconciliationReportService,
  bankAccountLabel,
} from '@bill-book/reporting-core';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  registerAmount,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'bankAccountId' | 'asOfDate';

/**
 * The Reconciliation Report (TK-202): its own page on its own endpoint, at the
 * path the menu links to. One bank account at the end of one day: the balance
 * in the books, the payments and receipts the bank has not yet shown, the
 * statement lines the books do not yet have, and the statement balance they
 * add up to beside the one the imported statements give.
 *
 * It opens on the first active bank account and today, and runs straight away.
 * A missing account or date shows at its field; a refusal, a failure or a
 * difference between the two balances shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bank-reconciliation-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './bank-reconciliation-report.page.html',
  styleUrl: './bank-reconciliation-report.page.scss',
})
export class BankReconciliationReportPage implements OnInit {
  private readonly report = inject(BankReconciliationReportService);
  private readonly formats = inject(FormatSettingsService);

  protected readonly filters = new FormGroup({
    bankAccountId: new FormControl<number | null>(null, Validators.required),
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
  });

  protected readonly accounts = signal<BankReconciliationAccount[]>([]);
  protected readonly accountsLoaded = signal(false);
  protected readonly result = signal<BankReconciliationReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.accounts().map((a) => ({ value: a.bankAccountId, label: bankAccountLabel(a) })),
  );

  protected readonly columns: ColumnDef[] = [
    { field: 'transactionDate', header: 'Date', width: '7.5rem' },
    { field: 'transactionNo', header: 'Number', width: '8rem' },
    { field: 'reference', header: 'Reference', width: '9rem' },
    { field: 'description', header: 'Description' },
    { field: 'amountIn', header: 'Money in', numeric: true, width: '9rem' },
    { field: 'amountOut', header: 'Money out', numeric: true, width: '9rem' },
  ];

  /** The failure, else no bank accounts to choose from, else a difference between the two balances. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }

    if (this.accountsLoaded() && this.accounts().length === 0) {
      return [{ tone: 'info', text: 'This branch has no bank accounts yet. Add one under Banking, import its statement, and reconcile it here.' }];
    }

    const report = this.result();
    if (!report || report.isReconciled) {
      return [];
    }

    if (report.statementBalance === null) {
      return [{ tone: 'info', text: 'No imported statement gives a balance on or before this day, so there is nothing to compare the books with yet. Import a statement under Banking.' }];
    }

    return [{
      tone: 'warning',
      text: `The statement differs from the books by ${registerAmount(report.difference ?? 0, this.formats.settings())}. Match or ignore the statement lines under Banking, or look for a payment or receipt keyed for the wrong amount or account.`,
    }];
  });

  protected totalIn(items: readonly { amountIn: number }[]): number {
    return items.reduce((sum, i) => sum + i.amountIn, 0);
  }

  protected totalOut(items: readonly { amountOut: number }[]): number {
    return items.reduce((sum, i) => sum + i.amountOut, 0);
  }

  ngOnInit(): void {
    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
      const control: AbstractControl = this.filters.controls[name];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadAccounts();
  }

  /** The words under a field: the server's refusal, else a missing value once touched. */
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
        await this.report.run({ bankAccountId: value.bankAccountId as number, asOfDate: value.asOfDate as string }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** The accounts for the filter; the first active one is chosen and run. */
  private async loadAccounts(): Promise<void> {
    try {
      const accounts = await this.report.accounts();
      this.accounts.set(accounts);
      const first = accounts.find((a) => a.isActive) ?? accounts[0];
      if (first) {
        this.filters.controls.bankAccountId.setValue(first.bankAccountId);
        await this.run();
      }
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.accountsLoaded.set(true);
    }
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Reconciliation Report on this branch.');
      return;
    }

    if (failure?.status === 404) {
      this.serverErrors.set({ bankAccountId: 'That bank account is not on this branch.' });
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

    this.failure.set(failure?.error?.message || 'The reconciliation could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
