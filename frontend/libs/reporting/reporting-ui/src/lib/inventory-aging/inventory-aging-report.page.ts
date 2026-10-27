import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  InventoryAgingItemDto,
  InventoryAgingReportDto,
  InventoryAgingReportRequest,
  InventoryAgingReportService,
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

type FilterName = 'asOfDate' | 'itemCategoryId' | 'warehouseId' | 'onlyWithStock' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-inventory-aging-report-page',
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
  templateUrl: './inventory-aging-report.page.html',
  styleUrl: './inventory-aging-report.page.scss',
})
export class InventoryAgingReportPageComponent implements OnInit {
  private readonly service = inject(InventoryAgingReportService);
  private readonly formatSettings = inject(FormatSettingsService);

  readonly filters = new FormGroup({
    asOfDate: new FormControl<string>(toIsoDate(new Date()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    itemCategoryId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    onlyWithStock: new FormControl<boolean>(true, { nonNullable: true }),
    search: new FormControl<string>(''),
    viewMode: new FormControl<'value' | 'quantity'>('value', { nonNullable: true }),
  });

  readonly busy = signal(false);
  readonly loadingMore = signal(false);
  readonly choicesLoaded = signal(false);
  readonly messages = signal<UiMessage[]>([]);
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly categoryOptions = signal<BbSelectOption<number>[]>([]);
  readonly warehouseOptions = signal<BbSelectOption<number>[]>([]);
  readonly viewModeOptions: BbSelectOption<string>[] = [
    { value: 'value', label: 'Valuation (Amount)' },
    { value: 'quantity', label: 'Stock Quantity' },
  ];

  readonly result = signal<InventoryAgingReportDto | null>(null);
  readonly items = signal<InventoryAgingItemDto[]>([]);
  readonly pageNumber = signal(1);
  readonly pageSize = 25;

  readonly viewMode = signal<'value' | 'quantity'>('value');
  readonly narrow = signal(typeof window !== 'undefined' ? window.innerWidth < 840 : false);

  @HostListener('window:resize')
  onResize(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 840);
    }
  }

  readonly currencyCode = computed(() => this.result()?.baseCurrencyCode ?? 'INR');

  readonly hasMore = computed(() => {
    const res = this.result();
    if (!res) return false;
    return this.items().length < res.totalItems;
  });

  readonly columns = computed<ColumnDef[]>(() => {
    const isVal = this.viewMode() === 'value';
    return [
      { field: 'itemCode', header: 'Item Code' },
      { field: 'itemName', header: 'Item Name' },
      { field: 'categoryName', header: 'Category' },
      { field: 'unitCode', header: 'Unit' },
      { field: 'total', header: isVal ? 'Total Value' : 'Total Qty', numeric: true },
      { field: 'bucket0To30', header: isVal ? '0-30 Days' : '0-30 (Qty)', numeric: true },
      { field: 'bucket31To60', header: isVal ? '31-60 Days' : '31-60 (Qty)', numeric: true },
      { field: 'bucket61To90', header: isVal ? '61-90 Days' : '61-90 (Qty)', numeric: true },
      { field: 'bucket91To180', header: isVal ? '91-180 Days' : '91-180 (Qty)', numeric: true },
      { field: 'bucketOver180', header: isVal ? '180+ Days' : '180+ (Qty)', numeric: true },
    ];
  });

  ngOnInit(): void {
    this.filters.controls.viewMode.valueChanges.subscribe((mode) => {
      if (mode) {
        this.viewMode.set(mode);
      }
    });

    void this.loadLookups();
    void this.run();
  }

  private async loadLookups(): Promise<void> {
    try {
      const [cats, warehouses] = await Promise.all([
        this.service.getCategories(),
        this.service.getWarehouses(),
      ]);

      this.categoryOptions.set(
        cats.map((c) => ({ value: c.itemCategoryId, label: c.categoryName })),
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
          text: err instanceof Error ? err.message : 'Failed to load categories or warehouses.',
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
    const req: InventoryAgingReportRequest = {
      asOfDate: value.asOfDate,
      itemCategoryId: value.itemCategoryId,
      warehouseId: value.warehouseId,
      onlyWithStock: value.onlyWithStock,
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
          text: err instanceof Error ? err.message : 'Failed to load Inventory Aging report.',
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
    const req: InventoryAgingReportRequest = {
      asOfDate: value.asOfDate,
      itemCategoryId: value.itemCategoryId,
      warehouseId: value.warehouseId,
      onlyWithStock: value.onlyWithStock,
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
          text: err instanceof Error ? err.message : 'Failed to load more items.',
        },
      ]);
    } finally {
      this.loadingMore.set(false);
    }
  }
}
