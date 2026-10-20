import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  SalaryIncrementReportOptions,
  SalaryIncrementReportResult,
  SalaryIncrementReportService,
  employeesIn,
  percentText,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridFooterDirective,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'departmentId' | 'employeeId';

/**
 * The Salary Increment Report (TK-198): its own page on its own endpoint, not
 * the shared report host. Salary revisions that take effect in a period, with
 * the annual CTC before and after, the increment in money and per cent, who
 * recorded it and why, and a footer of totals.
 *
 * It opens on this financial year and runs straight away. A filter's own
 * problem — a missing date, a to date before the from date — shows at that
 * field; anything the server refuses for another reason shows in the message
 * box. The grid becomes one card per revision on a phone, totals included.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-salary-increment-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DataGridFooterDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './salary-increment-report.page.html',
  styleUrl: './salary-increment-report.page.scss',
})
export class SalaryIncrementReportPage implements OnInit {
  private readonly report = inject(SalaryIncrementReportService);

  protected readonly columns: ColumnDef[] = [
    { field: 'employeeCode', header: 'Code' },
    { field: 'employeeName', header: 'Employee' },
    { field: 'department', header: 'Department / Designation' },
    { field: 'effectiveDate', header: 'Effective date' },
    { field: 'previousSalary', header: 'Previous CTC', numeric: true },
    { field: 'incrementAmount', header: 'Increment', numeric: true },
    { field: 'incrementPercent', header: 'Increment %', numeric: true },
    { field: 'revisedSalary', header: 'Revised CTC', numeric: true },
    { field: 'recordedBy', header: 'Recorded by / Remarks' },
  ];

  private readonly financialYear = rangeForPreset('this-financial-year');

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.financialYear.from, Validators.required),
    toDate: new FormControl<string | null>(this.financialYear.to, Validators.required),
    departmentId: new FormControl<number | null>(null),
    employeeId: new FormControl<number | null>(null),
  });

  protected readonly options = signal<SalaryIncrementReportOptions>({ departments: [], employees: [] });
  private readonly departmentId = signal<number | null>(null);

  protected readonly departmentOptions = computed<BbSelectOption<number>[]>(() =>
    this.options().departments.map((d) => ({ value: d.departmentId, label: d.name })),
  );

  protected readonly employeeOptions = computed<BbSelectOption<number>[]>(() =>
    employeesIn(this.options().employees, this.departmentId()).map((e) => ({
      value: e.employeeId,
      label: `${e.employeeCode} · ${e.name}`,
    })),
  );

  protected readonly result = signal<SalaryIncrementReportResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);

  /** A field the server refused, shown at that field until it is changed. */
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    return failure ? [{ tone: 'error', text: failure }] : [];
  });

  protected readonly percent = percentText;

  ngOnInit(): void {
    const { departmentId, employeeId } = this.filters.controls;

    // An employee outside the chosen department cannot match; drop them.
    departmentId.valueChanges.subscribe((id) => {
      this.departmentId.set(id);
      const chosen = employeeId.value;
      if (chosen && !employeesIn(this.options().employees, id).some((e) => e.employeeId === chosen)) {
        employeeId.setValue(null);
      }
    });

    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
      const control: AbstractControl = this.filters.controls[name];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadOptions();
    void this.run();
  }

  /** The words under a field: the server's refusal, else its first broken rule once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    if (control.errors['required']) {
      return `${label} is required.`;
    }

    return control.errors['range'] ? 'To date cannot be before the from date.' : `Check the ${label.toLowerCase()}.`;
  }

  async run(): Promise<void> {
    const { fromDate, toDate } = this.filters.controls;
    this.filters.markAllAsTouched();

    if (fromDate.value && toDate.value && toDate.value < fromDate.value) {
      toDate.setErrors({ range: true });
    }

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
          departmentId: value.departmentId,
          employeeId: value.employeeId,
        }),
      );
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadOptions(): Promise<void> {
    try {
      this.options.set(await this.report.options());
    } catch (err: unknown) {
      this.showFailure(err, 'The employees and departments could not be loaded, so the report cannot be narrowed to one.');
    }
  }

  /**
   * A field's refusal goes to that field; a refusal of the whole request, or
   * a failure, goes to the message box.
   */
  private showFailure(err: unknown, fallback = 'The report could not be run. Please try again.'): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Salary Increment Report on this branch.');
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

    this.failure.set(failure?.error?.message || fallback);
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
