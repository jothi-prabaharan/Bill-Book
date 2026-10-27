import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BatchTrackingStatusItemDto,
  BatchTrackingStatusReportDto,
  BatchTrackingStatusReportRequest,
  BatchTrackingStatusReportService,
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
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asOfDate' | 'itemId' | 'warehouseId' | 'expiringWithinDays' | 'expiredOnly' | 'withStockOnly' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-batch-tracking-status-report-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    BranchDateInputComponent,
    TextInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './batch-tracking-status-report.page.html',
  styleUrl: './batch-tracking-status-report.page.scss',
})
export class BatchTrackingStatusReportPageComponent implements OnInit {
  private readonly service = inject(BatchTrackingStatusReportService);

  readonly filters = new FormGroup({
    asOfDate: new FormControl<string>(toIsoDate(new Date()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    itemId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    expiringWithinDays: new FormControl<number | null>(null),
    expiredOnly: new FormControl<boolean>(false, { nonNullable: true }),
    withStockOnly: new FormControl<boolean>(true, { nonNullable: true }),
    search: new FormControl<string>(''),
  });

  readonly busy = signal(false);
  readonly loadingMore = signal(false);
  readonly choicesLoaded = signal(false);
  readonly messages = signal<UiMessage[]>([]);
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly itemOptions = signal<BbSelectOption<number>[]>([]);
  readonly warehouseOptions = signal<BbSelectOption<number>[]>([]);
  readonly expiryWindowOptions: BbSelectOption<number>[] = [
    { value: 0, label: 'All expiry dates' },
    { value: 30, label: 'Expiring in 30 days' },
    { value: 60, label: 'Expiring in 60 days' },
    { value: 90, label: 'Expiring in 90 days' },
    { value: 180, label: 'Expiring in 180 days' },
  ];

  readonly result = signal<BatchTrackingStatusReportDto | null>(null);
  readonly items = signal<BatchTrackingStatusItemDto[]>([]);
  readonly pageNumber = signal(1);
  readonly pageSize = 25;

  readonly narrow = signal(typeof window !== 'undefined' ? window.innerWidth < 840 : false);

  @HostListener('window:resize')
  onResize(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 840);
    }
  }

  readonly hasMore = computed(() => {
    const res = this.result();
    if (!res) return false;
    return this.items().length < res.totalItems;
  });

  readonly columns: ColumnDef[] = [
    { field: 'batchNumber', header: 'Batch No' },
    { field: 'itemCode', header: 'Item Code' },
    { field: 'itemName', header: 'Item Name' },
    { field: 'categoryName', header: 'Category' },
    { field: 'warehouseName', header: 'Warehouse' },
    { field: 'manufactureDate', header: 'Mfg Date' },
    { field: 'expiryDate', header: 'Expiry Date' },
    { field: 'receivedQuantity', header: 'Received', numeric: true },
    { field: 'availableQuantity', header: 'Available', numeric: true },
    { field: 'mrp', header: 'MRP', numeric: true },
  ];

  ngOnInit(): void {
    void this.loadLookups();
    void this.run();
  }

  private async loadLookups(): Promise<void> {
    try {
      const [items, warehouses] = await Promise.all([
        this.service.getItems(),
        this.service.getWarehouses(),
      ]);

      this.itemOptions.set(
        items.map((i) => ({ value: i.itemId, label: `${i.itemName} (${i.itemCode})` })),
      );

      this.warehouseOptions.set(
        warehouses.map((w) => ({
          value: w.warehouseId,
          label: `${w.warehouseName} (${w.warehouseCode})`,
        })),
      );

      this.choicesLoaded.set(true);
    } catch (err: unknown) {
      this.messages.set([
        {
          tone: 'error',
          text: err instanceof Error ? err.message : 'Failed to load items or warehouses.',
        },
      ]);
    }
  }

  err(name: FilterName, label: string): string {
    const serverErr = this.fieldErrors()[name];
    if (serverErr) return serverErr;
    const ctrl = this.filters.get(name);
    if (!ctrl || !ctrl.touched || !ctrl.errors) return '';
    if (ctrl.errors['required']) return `${label} is required.`;
    return '';
  }

  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    this.messages.set([]);
    this.fieldErrors.set({});

    if (this.filters.invalid) {
      return;
    }

    const value = this.filters.getRawValue();
    const expiryDays = value.expiringWithinDays && value.expiringWithinDays > 0 ? value.expiringWithinDays : null;

    const req: BatchTrackingStatusReportRequest = {
      asOfDate: value.asOfDate,
      itemId: value.itemId,
      warehouseId: value.warehouseId,
      expiringWithinDays: expiryDays,
      expiredOnly: value.expiredOnly,
      withStockOnly: value.withStockOnly,
      search: value.search,
      pageNumber: 1,
      pageSize: this.pageSize,
    };

    this.busy.set(true);
    try {
      const data = await this.service.getReport(req);
      this.result.set(data);
      this.items.set(data.items);
      this.pageNumber.set(1);
    } catch (err: unknown) {
      this.messages.set([
        {
          tone: 'error',
          text: err instanceof Error ? err.message : 'Failed to load Batch Tracking Status report.',
        },
      ]);
    } finally {
      this.busy.set(false);
    }
  }

  async loadMore(): Promise<void> {
    if (this.loadingMore() || !this.hasMore()) return;

    const nextPage = this.pageNumber() + 1;
    const value = this.filters.getRawValue();
    const expiryDays = value.expiringWithinDays && value.expiringWithinDays > 0 ? value.expiringWithinDays : null;

    const req: BatchTrackingStatusReportRequest = {
      asOfDate: value.asOfDate,
      itemId: value.itemId,
      warehouseId: value.warehouseId,
      expiringWithinDays: expiryDays,
      expiredOnly: value.expiredOnly,
      withStockOnly: value.withStockOnly,
      search: value.search,
      pageNumber: nextPage,
      pageSize: this.pageSize,
    };

    this.loadingMore.set(true);
    try {
      const data = await this.service.getReport(req);
      this.items.update((prev) => [...prev, ...data.items]);
      this.pageNumber.set(nextPage);
    } catch (err: unknown) {
      this.messages.set([
        {
          tone: 'error',
          text: err instanceof Error ? err.message : 'Failed to load more batches.',
        },
      ]);
    } finally {
      this.loadingMore.set(false);
    }
  }
}
