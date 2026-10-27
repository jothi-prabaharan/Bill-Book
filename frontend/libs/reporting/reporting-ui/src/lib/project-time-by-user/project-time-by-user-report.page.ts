import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ProjectTimeByUserLookupsResponse,
  ProjectTimeByUserReportRequest,
  ProjectTimeByUserReportResponse,
  ProjectTimeByUserReportService,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  SelectComponent,
  UiMessage,
  rangeForPreset,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'userId' | 'projectId' | 'groupBy';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-project-time-by-user-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DecimalPipe,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './project-time-by-user-report.page.html',
  styleUrl: './project-time-by-user-report.page.scss',
})
export class ProjectTimeByUserReportPageComponent implements OnInit {
  private readonly service = inject(ProjectTimeByUserReportService);
  private readonly initialRange = rangeForPreset('this-financial-year');

  protected readonly narrow = signal(false);
  protected readonly expandedGroups = signal<Set<string>>(new Set());

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.initialRange.from, Validators.required),
    toDate: new FormControl<string | null>(this.initialRange.to, Validators.required),
    userId: new FormControl<string | null>(null),
    projectId: new FormControl<number | null>(null),
    groupBy: new FormControl<'user' | 'project'>('user', Validators.required),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<ProjectTimeByUserReportResponse | null>(null);
  protected readonly lookups = signal<ProjectTimeByUserLookupsResponse | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  protected readonly userOptions = computed<readonly BbSelectOption<string>[]>(() => {
    const list = this.lookups()?.users ?? [];
    return list.map((u) => ({ value: u.userId, label: u.name }));
  });

  protected readonly projectOptions = computed<readonly BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.projects ?? [];
    return list.map((p) => ({ value: p.id, label: `${p.code} - ${p.name}` }));
  });

  protected readonly groupByOptions: readonly BbSelectOption<string>[] = [
    { value: 'user', label: 'Group by Team Member' },
    { value: 'project', label: 'Group by Project' },
  ];

  protected readonly itemColumns: ColumnDef[] = [
    { field: 'identity', header: 'Project / Task', width: '20rem' },
    { field: 'hours', header: 'Total Hours', numeric: true, width: '10rem' },
    { field: 'billableHours', header: 'Billable (h)', numeric: true, width: '10rem' },
    { field: 'billedHours', header: 'Billed (h)', numeric: true, width: '10rem' },
    { field: 'nonBillableHours', header: 'Non-Billable (h)', numeric: true, width: '10rem' },
    { field: 'utilisationPercent', header: 'Utilisation %', numeric: true, width: '9rem' },
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
    const from = this.filters.controls.fromDate.value;
    const to = this.filters.controls.toDate.value;

    this.fieldErrors.set({});
    this.failure.set(null);

    if (!from) {
      this.fieldErrors.set({ fromDate: 'From date is required.' });
      return;
    }
    if (!to) {
      this.fieldErrors.set({ toDate: 'To date is required.' });
      return;
    }
    if (to < from) {
      this.fieldErrors.set({ toDate: 'To date must not be earlier than from date.' });
      return;
    }

    this.loading.set(true);

    const req: ProjectTimeByUserReportRequest = {
      fromDate: from,
      toDate: to,
      userId: this.filters.controls.userId.value ?? undefined,
      projectId: this.filters.controls.projectId.value ?? undefined,
      groupBy: this.filters.controls.groupBy.value ?? 'user',
    };

    this.service.getReport(req).subscribe({
      next: (res) => {
        this.reportData.set(res);
        // Expand first group by default for immediate visibility
        if (res.groups.length > 0) {
          this.expandedGroups.set(new Set([res.groups[0].groupKey]));
        }
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
          this.failure.set(err.error?.detail || err.message || 'Failed to load Project Time by User report.');
        }
      },
    });
  }

  protected resetFilters(): void {
    this.filters.reset({
      fromDate: this.initialRange.from,
      toDate: this.initialRange.to,
      userId: null,
      projectId: null,
      groupBy: 'user',
    });
    this.fieldErrors.set({});
    this.failure.set(null);
    this.run();
  }

  protected toggleExpand(groupKey: string): void {
    const current = new Set(this.expandedGroups());
    if (current.has(groupKey)) {
      current.delete(groupKey);
    } else {
      current.add(groupKey);
    }
    this.expandedGroups.set(current);
  }

  protected isExpanded(groupKey: string): boolean {
    return this.expandedGroups().has(groupKey);
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

  protected getUtilisationClass(percent: number | null | undefined): string {
    if (percent === null || percent === undefined) return '';
    if (percent >= 75) return 'util-high';
    if (percent >= 50) return 'util-mid';
    return 'util-low';
  }

  protected isHighUtilisation(percent: number | null | undefined): boolean {
    return typeof percent === 'number' && percent >= 75;
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
