import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ProjectBudgetLookupsResponse,
  ProjectBudgetVsActualReportRequest,
  ProjectBudgetVsActualReportResponse,
  ProjectBudgetVsActualReportService,
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
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asOfDate' | 'status' | 'customerId' | 'projectId';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-project-budget-vs-actual-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DecimalPipe,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
  ],
  templateUrl: './project-budget-vs-actual-report.page.html',
  styleUrl: './project-budget-vs-actual-report.page.scss',
})
export class ProjectBudgetVsActualReportPageComponent implements OnInit {
  private readonly service = inject(ProjectBudgetVsActualReportService);

  protected readonly narrow = signal(false);
  protected readonly expandedProjects = signal<Set<number>>(new Set());

  protected readonly filters = new FormGroup({
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    status: new FormControl<string | null>(null),
    customerId: new FormControl<number | null>(null),
    projectId: new FormControl<number | null>(null),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<ProjectBudgetVsActualReportResponse | null>(null);
  protected readonly lookups = signal<ProjectBudgetLookupsResponse | null>(null);

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

  protected readonly projectOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.projects ?? [];
    return list.map((p) => ({ value: p.id, label: `${p.code} - ${p.name}` }));
  });

  protected readonly taskColumns: ColumnDef[] = [
    { field: 'taskName', header: 'Task Name', width: '16rem' },
    { field: 'budgetHours', header: 'Budget Hours', numeric: true, width: '10rem' },
    { field: 'actualHours', header: 'Actual Hours', numeric: true, width: '10rem' },
    { field: 'hoursVariance', header: 'Variance (h)', numeric: true, width: '10rem' },
    { field: 'hoursUsedPercent', header: '% Used', numeric: true, width: '8rem' },
    { field: 'status', header: 'Status', width: '8rem' },
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
      error: () => {},
    });
  }

  protected run(): void {
    this.fieldErrors.set({});
    this.failure.set(null);

    const asOf = this.filters.value.asOfDate;
    if (!asOf) {
      this.fieldErrors.set({ asOfDate: 'As of date is required.' });
      return;
    }

    const req: ProjectBudgetVsActualReportRequest = {
      asOfDate: asOf,
      status: this.filters.value.status ?? undefined,
      customerId: this.filters.value.customerId ?? undefined,
      projectId: this.filters.value.projectId ?? undefined,
    };

    this.loading.set(true);
    this.service.getReport(req).subscribe({
      next: (res) => {
        this.reportData.set(res);
        // Expand all projects by default so tasks are immediately visible
        const ids = new Set<number>(res.projects.map((p) => p.projectId));
        this.expandedProjects.set(ids);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 400 && err.error?.errors) {
          const apiErrors: Partial<Record<FilterName, string>> = {};
          const keys = Object.keys(err.error.errors);
          for (const key of keys) {
            const lower = key.toLowerCase();
            if (lower.includes('asof')) {
              apiErrors.asOfDate = err.error.errors[key][0];
            } else if (lower.includes('customer')) {
              apiErrors.customerId = err.error.errors[key][0];
            } else if (lower.includes('project')) {
              apiErrors.projectId = err.error.errors[key][0];
            }
          }
          this.fieldErrors.set(apiErrors);
        } else {
          this.failure.set(err.error?.title || err.message || 'Failed to load project budget vs actual report.');
        }
      },
    });
  }

  protected resetFilters(): void {
    this.filters.reset({
      asOfDate: toIsoDate(new Date()),
      status: null,
      customerId: null,
      projectId: null,
    });
    this.run();
  }

  protected toggleExpand(projectId: number): void {
    const current = new Set(this.expandedProjects());
    if (current.has(projectId)) {
      current.delete(projectId);
    } else {
      current.add(projectId);
    }
    this.expandedProjects.set(current);
  }

  protected isExpanded(projectId: number): boolean {
    return this.expandedProjects().has(projectId);
  }
}
