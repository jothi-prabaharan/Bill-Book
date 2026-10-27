import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ProjectUnbilledLookupsResponse,
  ProjectUnbilledProject,
  ProjectUnbilledWorkReportRequest,
  ProjectUnbilledWorkReportResponse,
  ProjectUnbilledWorkReportService,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asOfDate' | 'customerId' | 'projectId';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-project-unbilled-work-report-page',
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
    ReportDatePipe,
  ],
  templateUrl: './project-unbilled-work-report.page.html',
  styleUrl: './project-unbilled-work-report.page.scss',
})
export class ProjectUnbilledWorkReportPageComponent implements OnInit {
  private readonly service = inject(ProjectUnbilledWorkReportService);

  protected readonly narrow = signal(false);
  protected readonly expandedProjects = signal<Set<number>>(new Set());

  protected readonly filters = new FormGroup({
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    customerId: new FormControl<number | null>(null),
    projectId: new FormControl<number | null>(null),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<ProjectUnbilledWorkReportResponse | null>(null);
  protected readonly lookups = signal<ProjectUnbilledLookupsResponse | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  protected readonly customerOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.customers ?? [];
    return list.map((c) => ({ value: c.id, label: c.name }));
  });

  protected readonly projectOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.projects ?? [];
    return list.map((p) => ({ value: p.id, label: `${p.code} - ${p.name}` }));
  });

  protected readonly lineColumns: ColumnDef[] = [
    { field: 'lineType', header: 'Type', width: '7rem' },
    { field: 'date', header: 'Date', width: '9rem' },
    { field: 'reference', header: 'Reference', width: '11rem' },
    { field: 'description', header: 'Description', width: '16rem' },
    { field: 'quantity', header: 'Qty / Hours', numeric: true, width: '8rem' },
    { field: 'unitRate', header: 'Rate / Base', numeric: true, width: '10rem' },
    { field: 'markupPercent', header: 'Markup %', numeric: true, width: '8rem' },
    { field: 'amount', header: 'Amount', numeric: true, width: '10rem' },
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

  protected run(): void {
    const asOf = this.filters.controls.asOfDate.value;
    if (!asOf) {
      this.fieldErrors.set({ asOfDate: 'As of date is required.' });
      return;
    }

    this.loading.set(true);
    this.failure.set(null);
    this.fieldErrors.set({});

    const req: ProjectUnbilledWorkReportRequest = {
      asOfDate: asOf,
      customerId: this.filters.controls.customerId.value ?? undefined,
      projectId: this.filters.controls.projectId.value ?? undefined,
    };

    this.service.getReport(req).subscribe({
      next: (res) => {
        this.reportData.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 400 && err.error?.errors) {
          const fe: Partial<Record<FilterName, string>> = {};
          for (const key of Object.keys(err.error.errors)) {
            const camelKey = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
            fe[camelKey] = err.error.errors[key][0];
          }
          this.fieldErrors.set(fe);
        } else {
          this.failure.set(err.error?.detail || err.message || 'Failed to load Project Unbilled Work report.');
        }
      },
    });
  }

  protected resetFilters(): void {
    this.filters.reset({
      asOfDate: toIsoDate(new Date()),
      customerId: null,
      projectId: null,
    });
    this.fieldErrors.set({});
    this.failure.set(null);
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

  protected createInvoiceUrl(project: ProjectUnbilledProject): string {
    const customerParam = project.customerId ? `&customerId=${project.customerId}` : '';
    return `/sales/invoices/new?projectId=${project.projectId}${customerParam}`;
  }

  protected err(controlName: FilterName, label: string): string {
    const serverErr = this.fieldErrors()[controlName];
    if (serverErr) {
      return serverErr;
    }
    const ctrl = this.filters.get(controlName);
    if (ctrl && ctrl.touched && ctrl.invalid) {
      return `${label} is required.`;
    }
    return '';
  }

  private loadLookups(): void {
    this.service.getLookups().subscribe({
      next: (lookups) => this.lookups.set(lookups),
      error: () => {
        // Silently continue if lookups fail
      },
    });
  }

  private updateViewport(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 800);
    }
  }
}
