import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BatchTrackingWarehouseChoice,
  WarehouseTrackingCategoryChoice,
  WarehouseTrackingGroupDto,
  WarehouseTrackingStatusReportRequest,
  WarehouseTrackingStatusReportResultDto,
  WarehouseTrackingStatusReportService,
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

type FilterName = 'asOfDate' | 'warehouseId' | 'itemCategoryId' | 'showZeroBalance' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-warehouse-tracking-status-report-page',
  standalone: true,
  imports: [
    DecimalPipe,
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
  templateUrl: './warehouse-tracking-status-report.page.html',
  styleUrl: './warehouse-tracking-status-report.page.scss',
})
export class WarehouseTrackingStatusReportPageComponent implements OnInit {
  private readonly service = inject(WarehouseTrackingStatusReportService);

  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    asOfDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    warehouseId: new FormControl<number | null>(null),
    itemCategoryId: new FormControl<number | null>(null),
    showZeroBalance: new FormControl<boolean>(false),
    search: new FormControl<string>(''),
  });

  protected readonly warehouses = signal<BatchTrackingWarehouseChoice[]>([]);
  protected readonly categories = signal<WarehouseTrackingCategoryChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<WarehouseTrackingStatusReportResultDto | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({
      value: w.warehouseId,
      label: w.warehouseName,
    })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({
      value: c.itemCategoryId,
      label: c.categoryName,
    })),
  );

  protected readonly warehouseGroups = computed<WarehouseTrackingGroupDto[]>(() => {
    const res = this.result();
    return res ? res.warehouses : [];
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'itemCode', header: 'Item Code', width: '9rem' },
    { field: 'itemName', header: 'Item Name' },
    { field: 'categoryName', header: 'Category', width: '9rem' },
    { field: 'unitCode', header: 'Unit', width: '6rem' },
    { field: 'quantityIn', header: 'Qty Received', numeric: true, width: '8rem' },
    { field: 'quantityOut', header: 'Qty Issued', numeric: true, width: '8rem' },
    { field: 'availableQuantity', header: 'Available Qty', numeric: true, width: '8.5rem' },
    { field: 'averageCost', header: 'Branch Avg Cost', numeric: true, width: '9rem' },
    { field: 'estimatedValue', header: 'Estimated Value', numeric: true, width: '9.5rem' },
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
      const req: WarehouseTrackingStatusReportRequest = {
        asOfDate: asOfVal,
        warehouseId: this.filters.value.warehouseId ?? undefined,
        itemCategoryId: this.filters.value.itemCategoryId ?? undefined,
        showZeroBalance: this.filters.value.showZeroBalance ?? false,
        search: this.filters.value.search ? this.filters.value.search.trim() : undefined,
        pageNumber: 1,
        pageSize: 50,
      };

      const res = await this.service.getReport(req);
      this.result.set(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to run warehouse tracking status report.';
      this.failure.set(msg);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadFilterChoices(): Promise<void> {
    try {
      const [warehouses, categories] = await Promise.all([
        this.service.getWarehouses().catch(() => []),
        this.service.getCategories().catch(() => []),
      ]);
      this.warehouses.set(warehouses);
      this.categories.set(categories);
      this.choicesLoaded.set(true);
    } catch {
      this.choicesLoaded.set(true);
    }
  }
}
