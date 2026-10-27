import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  SerialTrackingItemChoice,
  SerialTrackingStatusItemDto,
  SerialTrackingStatusReportRequest,
  SerialTrackingStatusReportResultDto,
  SerialTrackingStatusReportService,
  SerialTrackingWarehouseChoice,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportDatePipe,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'asOfDate' | 'itemId' | 'warehouseId' | 'status' | 'search';

const STATUS_OPTIONS: BbSelectOption<string>[] = [
  { value: 'All', label: 'All Statuses' },
  { value: 'InStock', label: 'In Stock' },
  { value: 'Sold', label: 'Sold' },
  { value: 'Returned', label: 'Returned' },
  { value: 'Scrapped', label: 'Written Off / Scrapped' },
];

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-serial-tracking-status-report-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    TextInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportDatePipe,
  ],
  templateUrl: './serial-tracking-status-report.page.html',
  styleUrl: './serial-tracking-status-report.page.scss',
})
export class SerialTrackingStatusReportPageComponent implements OnInit {
  private readonly service = inject(SerialTrackingStatusReportService);

  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    itemId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    status: new FormControl<string>('All'),
    search: new FormControl<string | null>(null),
  });

  protected readonly statusOptions = STATUS_OPTIONS;

  protected readonly items = signal<SerialTrackingItemChoice[]>([]);
  protected readonly warehouses = signal<SerialTrackingWarehouseChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<SerialTrackingStatusReportResultDto | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({
      value: i.itemId,
      label: `${i.itemCode} - ${i.itemName}`,
    })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({
      value: w.warehouseId,
      label: w.warehouseName,
    })),
  );

  protected readonly serials = computed<SerialTrackingStatusItemDto[]>(() => {
    const res = this.result();
    return res ? res.items : [];
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'serialNumber', header: 'Serial No', width: '9rem' },
    { field: 'itemCode', header: 'Item Code', width: '8rem' },
    { field: 'itemName', header: 'Item Name' },
    { field: 'status', header: 'Status', width: '7.5rem' },
    { field: 'warehouseName', header: 'Warehouse', width: '8.5rem' },
    { field: 'receivedDate', header: 'Received On', width: '9.5rem' },
    { field: 'soldDate', header: 'Sold To / Date', width: '12rem' },
    { field: 'expiryDate', header: 'Expiry / Warranty', width: '8.5rem' },
  ];

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
    await this.loadFilterChoices();
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

    const asOfVal = this.filters.value.asOfDate;
    if (!asOfVal) {
      this.filters.get('asOfDate')?.markAsTouched();
      return;
    }

    this.busy.set(true);
    try {
      const statusVal = this.filters.value.status;
      const req: SerialTrackingStatusReportRequest = {
        asOfDate: asOfVal,
        itemId: this.filters.value.itemId ?? undefined,
        warehouseId: this.filters.value.warehouseId ?? undefined,
        status: statusVal && statusVal !== 'All' ? statusVal : undefined,
        search: this.filters.value.search ?? undefined,
        pageNumber: 1,
        pageSize: 100,
      };

      const res = await this.service.getReport(req);
      this.result.set(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to run serial tracking status report.';
      this.failure.set(msg);
    } finally {
      this.busy.set(false);
    }
  }

  protected setStatusFilter(status: string): void {
    this.filters.patchValue({ status });
    void this.run();
  }

  private async loadFilterChoices(): Promise<void> {
    try {
      const [items, warehouses] = await Promise.all([
        this.service.getItems().catch(() => []),
        this.service.getWarehouses().catch(() => []),
      ]);
      this.items.set(items);
      this.warehouses.set(warehouses);
      this.choicesLoaded.set(true);
    } catch {
      this.choicesLoaded.set(true);
    }
  }
}
