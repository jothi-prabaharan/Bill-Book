import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { UpperCasePipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  DisposalScheduleCategoryGroupDto,
  DisposalScheduleLookupsDto,
  DisposalScheduleReportRequest,
  DisposalScheduleReportResponse,
  DisposalScheduleReportService,
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
  TextInputComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'categoryId' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-disposal-schedule-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    UpperCasePipe,
    BranchDateInputComponent,
    SelectComponent,
    TextInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './disposal-schedule-report.page.html',
  styleUrl: './disposal-schedule-report.page.scss',
})
export class DisposalScheduleReportPageComponent implements OnInit {
  private readonly service = inject(DisposalScheduleReportService);

  protected readonly narrow = signal(false);

  private readonly initialRange = rangeForPreset('this-financial-year');

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.initialRange.from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    categoryId: new FormControl<number>(0),
    search: new FormControl<string | null>(null),
  });

  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<FilterName, string>>>({});
  protected readonly reportData = signal<DisposalScheduleReportResponse | null>(null);
  protected readonly lookups = signal<DisposalScheduleLookupsDto | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() => {
    const list = this.lookups()?.categories ?? [];
    return [
      { label: 'All Categories', value: 0 },
      ...list.map((c) => ({
        label: c.disposedCount > 0 ? `${c.name} (${c.disposedCount} disposed)` : c.name,
        value: c.id,
      })),
    ];
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'assetCode', header: 'Asset Code', width: '8.5rem' },
    { field: 'assetName', header: 'Asset Name', width: '14rem' },
    { field: 'purchaseDate', header: 'Purchased', width: '7.5rem' },
    { field: 'cost', header: 'Cost', numeric: true, width: '8.5rem' },
    { field: 'disposalDate', header: 'Disposal Date', width: '8rem' },
    { field: 'accumDep', header: 'Accum Dep', numeric: true, width: '8.5rem' },
    { field: 'nbvAtDisposal', header: 'Book Value', numeric: true, width: '8.5rem' },
    { field: 'saleProceeds', header: 'Proceeds', numeric: true, width: '8.5rem' },
    { field: 'gainOnDisposal', header: 'Gain', numeric: true, width: '7.5rem' },
    { field: 'lossOnDisposal', header: 'Loss', numeric: true, width: '7.5rem' },
    { field: 'reference', header: 'Disposal Notes / Ref', width: '12rem' },
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

  private loadLookups(): void {
    this.service.getLookups().subscribe({
      next: (data) => this.lookups.set(data),
      error: () => {
        // Lookups failure does not stop the report
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

    const catId = this.filters.value.categoryId;
    const req: DisposalScheduleReportRequest = {
      from: from!,
      to: to!,
      categoryId: catId && catId > 0 ? catId : null,
      search: this.filters.value.search ?? null,
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
            } else if (lower.includes('category')) {
              apiErrors.categoryId = err.error.errors[key][0];
            }
          }
          this.fieldErrors.set(apiErrors);
        } else {
          this.failure.set(err.error?.detail || err.error?.message || 'Failed to load disposal schedule report.');
        }
      },
    });
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

  protected categorySubtotal(cat: DisposalScheduleCategoryGroupDto): string {
    return `${cat.categoryName} (${cat.assetCount} disposed)`;
  }
}
