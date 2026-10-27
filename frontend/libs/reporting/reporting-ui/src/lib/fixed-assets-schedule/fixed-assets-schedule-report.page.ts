import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  FixedAssetScheduleCategoryGroupDto,
  FixedAssetsScheduleLookupsDto,
  FixedAssetsScheduleReportRequest,
  FixedAssetsScheduleReportResponse,
  FixedAssetsScheduleReportService,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
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

type FilterName = 'fromDate' | 'toDate' | 'categoryId' | 'status' | 'book' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fixed-assets-schedule-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    TextInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './fixed-assets-schedule-report.page.html',
  styleUrl: './fixed-assets-schedule-report.page.scss',
})
export class FixedAssetsScheduleReportPageComponent implements OnInit {
  private readonly service = inject(FixedAssetsScheduleReportService);

  protected readonly narrow = signal(false);

  private readonly initialRange = rangeForPreset('this-financial-year');

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(this.initialRange.from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    categoryId: new FormControl<number | null>(null),
    status: new FormControl<string>('All'),
    book: new FormControl<string>('Books'),
    search: new FormControl<string>(''),
    showDisposalColumns: new FormControl<boolean>(false),
  });

  protected readonly lookups = signal<FixedAssetsScheduleLookupsDto>({
    categories: [],
    statuses: ['All', 'Active', 'Disposed'],
    books: ['Books', 'Tax'],
  });

  protected readonly result = signal<FixedAssetsScheduleReportResponse | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() => [
    { value: 0, label: 'All Categories' },
    ...this.lookups().categories.map((c) => ({
      value: c.id,
      label: c.name,
    })),
  ]);

  protected readonly statusOptions = computed<BbSelectOption<string>[]>(() =>
    this.lookups().statuses.map((s) => ({
      value: s,
      label: s,
    })),
  );

  protected readonly bookOptions = computed<BbSelectOption<string>[]>(() =>
    this.lookups().books.map((b) => ({
      value: b,
      label: b === 'Books' ? 'Books (Financial)' : 'Tax Schedule',
    })),
  );

  protected readonly categories = computed<FixedAssetScheduleCategoryGroupDto[]>(() => {
    const res = this.result();
    return res ? res.categories : [];
  });

  protected readonly columns = computed<ColumnDef[]>(() => {
    const base: ColumnDef[] = [
      { field: 'assetCode', header: 'Asset Code', width: '8.5rem' },
      { field: 'assetName', header: 'Asset Name' },
      { field: 'purchaseDate', header: 'Purchased', width: '7.5rem' },
      { field: 'openingCost', header: 'Opening Cost', numeric: true, width: '8.5rem' },
      { field: 'additionCost', header: 'Additions', numeric: true, width: '8rem' },
      { field: 'disposalCost', header: 'Disposals', numeric: true, width: '8rem' },
      { field: 'closingCost', header: 'Closing Cost', numeric: true, width: '9rem' },
      { field: 'openingAccumDep', header: 'Opening Dep', numeric: true, width: '8.5rem' },
      { field: 'depreciation', header: 'Dep Charged', numeric: true, width: '8.5rem' },
      { field: 'disposalAccumDep', header: 'Written Back', numeric: true, width: '8.5rem' },
      { field: 'closingAccumDep', header: 'Closing Dep', numeric: true, width: '9rem' },
      { field: 'openingNbv', header: 'Opening NBV', numeric: true, width: '8.5rem' },
      { field: 'closingNbv', header: 'Closing NBV', numeric: true, width: '9.5rem' },
    ];

    if (this.filters.value.showDisposalColumns) {
      base.push(
        { field: 'disposalDate', header: 'Disposal Date', width: '8rem' },
        { field: 'saleProceeds', header: 'Proceeds', numeric: true, width: '8.5rem' },
        { field: 'gainLoss', header: 'Gain / Loss', numeric: true, width: '8.5rem' },
      );
    }

    base.push({ field: 'status', header: 'Status', width: '7rem' });
    return base;
  });

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  @HostListener('window:resize')
  onResize(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 800);
    }
  }

  ngOnInit(): void {
    this.onResize();
    void this.init();
  }

  private async init(): Promise<void> {
    await this.loadLookups();
    await this.run();
  }

  protected err(field: FilterName, label: string): string {
    const srv = this.serverErrors()[field];
    if (srv) {
      return srv;
    }
    const ctrl = this.filters.get(field);
    if (!ctrl || !ctrl.touched || ctrl.valid) {
      return '';
    }
    if (ctrl.hasError('required')) {
      return `${label} is required.`;
    }
    return 'Invalid value.';
  }

  protected async run(): Promise<void> {
    this.serverErrors.set({});
    this.failure.set(null);

    const fromVal = this.filters.value.fromDate;
    const toVal = this.filters.value.toDate;

    let hasClientErrors = false;
    if (!fromVal) {
      this.filters.get('fromDate')?.markAsTouched();
      hasClientErrors = true;
    }
    if (!toVal) {
      this.filters.get('toDate')?.markAsTouched();
      hasClientErrors = true;
    }
    if (fromVal && toVal && toVal < fromVal) {
      this.serverErrors.update((e) => ({ ...e, toDate: 'To date cannot be earlier than From date.' }));
      hasClientErrors = true;
    }

    if (hasClientErrors) {
      return;
    }

    this.busy.set(true);
    try {
      const catVal = this.filters.value.categoryId;
      const req: FixedAssetsScheduleReportRequest = {
        from: fromVal!,
        to: toVal!,
        categoryId: catVal && catVal > 0 ? catVal : undefined,
        status: this.filters.value.status ?? undefined,
        book: this.filters.value.book ?? undefined,
        search: this.filters.value.search ? this.filters.value.search.trim() : undefined,
      };

      const res = await this.service.getReport(req);
      this.result.set(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to run fixed assets schedule report.';
      this.failure.set(msg);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadLookups(): Promise<void> {
    try {
      const lookups = await this.service.getLookups();
      this.lookups.set(lookups);
    } catch {
      // Fallback defaults in signal
    }
  }
}
