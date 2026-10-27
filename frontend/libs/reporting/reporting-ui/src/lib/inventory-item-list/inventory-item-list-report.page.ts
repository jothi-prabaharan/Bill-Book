import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  InventoryItemListItemDto,
  InventoryItemListReportDto,
  InventoryItemListReportRequest,
  InventoryItemListReportService,
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

type FilterName = 'asOfDate' | 'itemCategoryId' | 'status' | 'itemType' | 'onlyWithStock' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-inventory-item-list-page',
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
  templateUrl: './inventory-item-list-report.page.html',
  styleUrl: './inventory-item-list-report.page.scss',
})
export class InventoryItemListPageComponent implements OnInit {
  private readonly service = inject(InventoryItemListReportService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly formatSettings = inject(FormatSettingsService);

  readonly filters = new FormGroup({
    asOfDate: new FormControl<string>(toIsoDate(new Date()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    itemCategoryId: new FormControl<number | null>(null),
    status: new FormControl<string>('All'),
    itemType: new FormControl<string>('All'),
    onlyWithStock: new FormControl<boolean>(false, { nonNullable: true }),
    search: new FormControl<string>(''),
  });

  readonly busy = signal(false);
  readonly loadingMore = signal(false);
  readonly choicesLoaded = signal(false);
  readonly messages = signal<UiMessage[]>([]);
  readonly fieldErrors = signal<Record<string, string>>({});

  readonly categoryOptions = signal<BbSelectOption<number>[]>([]);
  readonly statusOptions = signal<BbSelectOption<string>[]>([
    { value: 'All', label: 'All statuses' },
    { value: 'Active', label: 'Active' },
    { value: 'Inactive', label: 'Inactive' },
  ]);
  readonly itemTypeOptions = signal<BbSelectOption<string>[]>([
    { value: 'All', label: 'All item types' },
    { value: 'Inventory', label: 'Inventory Item' },
    { value: 'NonInventory', label: 'Non-Inventory Item' },
    { value: 'Service', label: 'Service' },
  ]);

  readonly result = signal<InventoryItemListReportDto | null>(null);
  readonly items = signal<InventoryItemListItemDto[]>([]);
  readonly pageNumber = signal(1);
  readonly pageSize = 25;

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

  readonly columns: ColumnDef[] = [
    { field: 'itemCode', header: 'Item Code' },
    { field: 'itemName', header: 'Item Name' },
    { field: 'categoryName', header: 'Category' },
    { field: 'itemType', header: 'Type' },
    { field: 'unitCode', header: 'Unit' },
    { field: 'status', header: 'Status' },
    { field: 'quantityOnHand', header: 'On Hand', numeric: true },
    { field: 'averageCost', header: 'Avg Cost', numeric: true },
    { field: 'unitCostPriceFifo', header: 'FIFO Cost', numeric: true },
    { field: 'totalValue', header: 'Total Value', numeric: true },
    { field: 'quantityOnOrder', header: 'On Order', numeric: true },
    { field: 'quantityCommitted', header: 'Committed', numeric: true },
    { field: 'balanceQty', header: 'Available', numeric: true },
  ];

  ngOnInit(): void {
    void this.loadLookups();
    void this.run();
  }

  private async loadLookups(): Promise<void> {
    try {
      const [cats, types, statuses] = await Promise.all([
        this.service.getCategories(),
        this.service.getItemTypes(),
        this.service.getStatuses(),
      ]);

      this.categoryOptions.set(
        cats.map((c) => ({ value: c.itemCategoryId, label: c.categoryName })),
      );

      if (types.length > 0) {
        this.itemTypeOptions.set([
          { value: 'All', label: 'All item types' },
          ...types.map((t) => ({ value: t.key, label: t.label })),
        ]);
      }

      if (statuses.length > 0) {
        this.statusOptions.set([
          { value: 'All', label: 'All statuses' },
          ...statuses.map((s) => ({ value: s.key, label: s.label })),
        ]);
      }

      this.choicesLoaded.set(true);
    } catch {
      this.choicesLoaded.set(true);
    }
  }

  err(field: FilterName, label: string): string {
    const serverErr = this.fieldErrors()[field];
    if (serverErr) return serverErr;
    const ctrl = this.filters.get(field);
    if (!ctrl || !ctrl.touched || ctrl.valid) return '';
    if (ctrl.errors?.['required']) return `${label} is required.`;
    return '';
  }

  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    this.fieldErrors.set({});
    this.messages.set([]);

    if (this.filters.invalid) {
      return;
    }

    this.busy.set(true);
    this.pageNumber.set(1);

    const val = this.filters.getRawValue();
    const req: InventoryItemListReportRequest = {
      asOfDate: val.asOfDate,
      itemCategoryId: val.itemCategoryId,
      status: val.status,
      itemType: val.itemType,
      onlyWithStock: val.onlyWithStock,
      search: val.search,
      pageNumber: 1,
      pageSize: this.pageSize,
    };

    try {
      const res = await this.service.getReport(req);
      this.result.set(res);
      this.items.set(res.items);
    } catch (err: unknown) {
      this.messages.set([
        {
          tone: 'error',
          text: (err as Error)?.message || 'Failed to load Inventory Item List.',
        },
      ]);
    } finally {
      this.busy.set(false);
    }
  }

  async loadMore(): Promise<void> {
    if (this.loadingMore() || !this.hasMore()) return;

    this.loadingMore.set(true);
    const nextPage = this.pageNumber() + 1;
    const val = this.filters.getRawValue();
    const req: InventoryItemListReportRequest = {
      asOfDate: val.asOfDate,
      itemCategoryId: val.itemCategoryId,
      status: val.status,
      itemType: val.itemType,
      onlyWithStock: val.onlyWithStock,
      search: val.search,
      pageNumber: nextPage,
      pageSize: this.pageSize,
    };

    try {
      const res = await this.service.getReport(req);
      this.pageNumber.set(nextPage);
      this.items.update((curr) => [...curr, ...res.items]);
    } catch (err: unknown) {
      this.messages.set([
        {
          tone: 'error',
          text: (err as Error)?.message || 'Failed to load more items.',
        },
      ]);
    } finally {
      this.loadingMore.set(false);
    }
  }
}
