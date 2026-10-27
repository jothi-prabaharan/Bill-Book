import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ProjectProfitabilityLookups,
  ProjectProfitabilityReportRequest,
  ProjectProfitabilityReportResponse,
  ProjectProfitabilityReportService,
  ProjectProfitabilityRow,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'customerId' | 'status' | 'billingMethod' | 'projectId';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-project-profitability-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    DecimalPipe,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
  ],
  templateUrl: './project-profitability-report.page.html',
  styleUrl: './project-profitability-report.page.scss',
})
export class ProjectProfitabilityReportPageComponent implements OnInit {
  private readonly service = inject(ProjectProfitabilityReportService);
  private readonly router = inject(Router);

  protected readonly narrow = signal(false);

  private readonly initialRange = rangeForPreset('this-financial-year');

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.initialRange.from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    customerId: new FormControl<number | null>(null),
    status: new FormControl<string | null>(null),
    billingMethod: new FormControl<string | null>(null),
    projectId: new FormControl<number | null>(null),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<ProjectProfitabilityReportResponse | null>(null);
  protected readonly lookups = signal<ProjectProfitabilityLookups | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  protected readonly customerOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.customers ?? [];
    return list.map((c) => ({ value: c.id, label: c.name }));
  });

  protected readonly statusOptions = computed<readonly BbSelectOption<string>[]>(() => {
    const list = this.lookups()?.statuses ?? ['Active', 'Completed', 'OnHold', 'Cancelled'];
    return list.map((s) => ({ value: s, label: s }));
  });

  protected readonly billingMethodOptions = computed<readonly BbSelectOption<string>[]>(() => {
    const list = this.lookups()?.billingMethods ?? ['FixedFee', 'TimeAndMaterials', 'NonBillable'];
    return list.map((b) => ({ value: b, label: b }));
  });

  protected readonly projectOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.projects ?? [];
    return list.map((p) => ({ value: p.id, label: `${p.code} - ${p.name}` }));
  });

  protected readonly gridColumns: ColumnDef[] = [
    { field: 'projectCode', header: 'Project Code', width: '9rem' },
    { field: 'projectName', header: 'Project Name', width: '13rem' },
    { field: 'customer', header: 'Customer', width: '12rem' },
    { field: 'status', header: 'Status', width: '7rem' },
    { field: 'billingMethod', header: 'Billing Method', width: '10rem' },
    { field: 'revenue', header: 'Revenue', numeric: true, width: '9rem' },
    { field: 'cost', header: 'Cost', numeric: true, width: '9rem' },
    { field: 'profit', header: 'Profit', numeric: true, width: '9rem' },
    { field: 'marginPercent', header: 'Margin %', numeric: true, width: '7rem' },
    { field: 'budgetAmount', header: 'Budget', numeric: true, width: '9rem' },
    { field: 'actions', header: '', width: '5rem' },
  ];

  ngOnInit(): void {
    this.updateViewport();
    this.loadLookups();
    this.run();
  }

  @HostListener('window:resize')
  protected onResize(): void {
    this.updateViewport();
  }

  private updateViewport(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 800);
    }
  }

  protected err(field: FilterName, label: string): string {
    const apiErr = this.fieldErrors()[field];
    if (apiErr) {
      return apiErr;
    }
    const ctrl = this.filters.get(field);
    if (ctrl?.hasError('required') && ctrl.touched) {
      return `${label} is required.`;
    }
    return '';
  }

  private loadLookups(): void {
    this.service.getLookups().subscribe({
      next: (res) => this.lookups.set(res),
      error: () => {
        // Fallback to empty lookups, report execution will still work
      },
    });
  }

  protected run(): void {
    this.fieldErrors.set({});
    this.failure.set(null);

    const from = this.filters.value.fromDate;
    const to = this.filters.value.toDate;

    const errors: Partial<Record<FilterName, string>> = {};
    if (!from) {
      errors.fromDate = 'From date is required.';
    }
    if (!to) {
      errors.toDate = 'To date is required.';
    }
    if (from && to && to < from) {
      errors.toDate = 'To date cannot be earlier than From date.';
    }

    if (Object.keys(errors).length > 0) {
      this.fieldErrors.set(errors);
      return;
    }

    const req: ProjectProfitabilityReportRequest = {
      from: from!,
      to: to!,
      customerId: this.filters.value.customerId ?? undefined,
      status: this.filters.value.status ?? undefined,
      billingMethod: this.filters.value.billingMethod ?? undefined,
      projectId: this.filters.value.projectId ?? undefined,
    };

    this.loading.set(true);
    this.service.getReport(req).subscribe({
      next: (res) => {
        this.reportData.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 400 && err.error?.errors) {
          const apiErrors: Partial<Record<FilterName, string>> = {};
          const keys = Object.keys(err.error.errors);
          for (const key of keys) {
            const lower = key.toLowerCase();
            if (lower.includes('from')) {
              apiErrors.fromDate = err.error.errors[key][0];
            } else if (lower.includes('to')) {
              apiErrors.toDate = err.error.errors[key][0];
            } else if (lower.includes('customer')) {
              apiErrors.customerId = err.error.errors[key][0];
            } else if (lower.includes('project')) {
              apiErrors.projectId = err.error.errors[key][0];
            }
          }
          this.fieldErrors.set(apiErrors);
        } else {
          this.failure.set(err.error?.title || err.message || 'Failed to load project profitability report.');
        }
      },
    });
  }

  protected resetFilters(): void {
    this.filters.reset({
      fromDate: this.initialRange.from,
      toDate: toIsoDate(new Date()),
      customerId: null,
      status: null,
      billingMethod: null,
      projectId: null,
    });
    this.run();
  }

  protected drillToBudgetVsActual(row: ProjectProfitabilityRow): void {
    void this.router.navigate(['/reports/budget-vs-actual-by-project'], {
      queryParams: { projectId: row.projectId },
    });
  }
}
