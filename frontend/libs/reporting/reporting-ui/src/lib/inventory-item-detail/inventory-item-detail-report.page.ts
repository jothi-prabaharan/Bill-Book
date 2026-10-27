import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  InventoryItemDetailGridRow,
  InventoryItemDetailGroup,
  InventoryItemDetailResult,
  InventoryItemDetailService,
  PayableInvoiceCategoryChoice,
  PayableInvoiceItemChoice,
  SalesAnalysisWarehouseChoice,
  appendInventoryItemDetailPage,
  categoryChoiceLabel,
  costingMethodLabel,
  hasMoreInventoryItemDetail,
  inventoryItemDetailSentence,
  itemChoiceLabel,
  itemDetailRows,
  warehouseChoiceLabel,
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
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'itemId' | 'itemCategoryId' | 'warehouseId' | 'source';

export interface ItemBlock {
  item: InventoryItemDetailGroup;
  rows: InventoryItemDetailGridRow[];
}

const SOURCE_OPTIONS: BbSelectOption<string>[] = [
  { value: 'Receipt', label: 'Receipt' },
  { value: 'Issue', label: 'Issue' },
  { value: 'Adjustment', label: 'Stock Adjustment' },
  { value: 'Opening', label: 'Opening Balance' },
  { value: 'TransferIn', label: 'Transfer In' },
  { value: 'TransferOut', label: 'Transfer Out' },
  { value: 'SalesReturn', label: 'Sales Return' },
  { value: 'PurchaseReturn', label: 'Purchase Return' },
];

/**
 * The Inventory Item Detail (TK-244): stock card for each stocked item over a period.
 * For each stocked item: opening balance, all stock movements in the period with document number,
 * source, contact, quantity in/out, unit cost, value, unit sale price, margin, running quantity
 * and value, and closing balance.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-inventory-item-detail-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './inventory-item-detail-report.page.html',
  styleUrl: './inventory-item-detail-report.page.scss',
})
export class InventoryItemDetailPage implements OnInit {
  private readonly report = inject(InventoryItemDetailService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    itemId: new FormControl<number | null>(null),
    itemCategoryId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    source: new FormControl<string | null>(null),
  });

  protected readonly items = signal<PayableInvoiceItemChoice[]>([]);
  protected readonly categories = signal<PayableInvoiceCategoryChoice[]>([]);
  protected readonly warehouses = signal<SalesAnalysisWarehouseChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<InventoryItemDetailResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly sourceOptions = SOURCE_OPTIONS;

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({ value: i.itemId, label: itemChoiceLabel(i) })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.itemCategoryId, label: categoryChoiceLabel(c) })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({ value: w.warehouseId, label: warehouseChoiceLabel(w) })),
  );

  protected readonly blocks = computed<ItemBlock[]>(() => {
    const res = this.result();
    if (!res) {
      return [];
    }
    return res.items.map((item) => ({
      item,
      rows: itemDetailRows(item, res.fromDate, res.toDate),
    }));
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'date', header: 'Date', width: '6.5rem' },
    { field: 'documentNo', header: 'Document', width: '8rem' },
    { field: 'source', header: 'Source', width: '8.5rem' },
    { field: 'contactName', header: 'Contact' },
    { field: 'warehouseName', header: 'Warehouse', width: '8rem' },
    { field: 'quantityIn', header: 'Qty In', numeric: true, width: '6rem' },
    { field: 'quantityOut', header: 'Qty Out', numeric: true, width: '6rem' },
    { field: 'unitCost', header: 'Unit Cost', numeric: true, width: '6.5rem' },
    { field: 'value', header: 'Value', numeric: true, width: '7rem' },
    { field: 'unitSalePrice', header: 'Sale Price', numeric: true, width: '6.5rem' },
    { field: 'margin', header: 'Margin', numeric: true, width: '7.5rem' },
    { field: 'runningQuantity', header: 'Running Qty', numeric: true, width: '6.5rem' },
    { field: 'runningValue', header: 'Running Val', numeric: true, width: '7.5rem' },
  ];

  protected readonly more = computed(() => {
    const res = this.result();
    return res ? hasMoreInventoryItemDetail(res) : false;
  });

  protected readonly messages = computed<UiMessage[]>(() => {
    const list: UiMessage[] = [];
    const fail = this.failure();
    if (fail) {
      list.push({ tone: 'error', text: fail });
    }

    const res = this.result();
    if (res && res.totalItems > 0) {
      list.push({
        tone: 'info',
        text: inventoryItemDetailSentence(res, (val) => this.formats.formatMoney(val)),
      });
    }

    return list;
  });

  protected readonly quantity = (val: number | null | undefined): string =>
    val !== null && val !== undefined ? this.formats.formatQuantity(val) : '—';

  protected readonly costingLabel = costingMethodLabel;

  protected readonly rowClass = (row: InventoryItemDetailGridRow): string =>
    row.kind === 'opening'
      ? 'row--opening'
      : row.kind === 'closing'
        ? 'row--closing'
        : row.costPending
          ? 'row--pending'
          : '';

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls)) {
      const control: AbstractControl = this.filters.controls[name as keyof typeof this.filters.controls];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadChoices();
    void this.run();
  }

  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : '';
  }

  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      this.result.set(await this.report.run(this.request(1)));
    } catch (err: unknown) {
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreInventoryItemDetail(loaded)) {
      return;
    }

    this.busy.set(true);
    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendInventoryItemDetailPage(loaded, next));
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private request(pageNumber: number) {
    const val = this.filters.getRawValue();
    return {
      fromDate: val.fromDate!,
      toDate: val.toDate!,
      itemId: val.itemId,
      itemCategoryId: val.itemCategoryId,
      warehouseId: val.warehouseId,
      source: val.source,
      pageNumber,
      pageSize: 25,
    };
  }

  private async loadChoices(): Promise<void> {
    try {
      const [items, categories, warehouses] = await Promise.all([
        this.report.items(),
        this.report.itemCategories(),
        this.report.warehouses(),
      ]);
      this.items.set(items);
      this.categories.set(categories);
      this.warehouses.set(warehouses);
      this.choicesLoaded.set(true);
    } catch {
      // Lookups failed; dropdowns will display empty options
    }
  }

  private clearServerError(field: string): void {
    const current = this.serverErrors();
    if (field in current) {
      const next = { ...current };
      delete next[field as FilterName];
      this.serverErrors.set(next);
    }
  }

  private showFailure(err: unknown): void {
    if (err && typeof err === 'object' && 'status' in err && (err as { status: number }).status === 400 && 'error' in err) {
      const errorObj = (err as { error: { errors?: Record<string, string[]> } }).error;
      if (errorObj?.errors) {
        const next: Partial<Record<FilterName, string>> = {};
        for (const [key, msgs] of Object.entries(errorObj.errors)) {
          const lower = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
          if (msgs.length > 0) {
            next[lower] = msgs[0];
          }
        }
        this.serverErrors.set(next);
        return;
      }
    }
    this.failure.set('Unable to run the Inventory Item Detail report right now. Please try again.');
  }
}
