import { ChangeDetectionStrategy } from '@angular/core';
import { TextInputComponent , NumberInputComponent , SearchInputComponent, DataGridComponent, DataGridCellTemplateDirective, ColumnDef } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

type Profile = 'Standard' | 'Pharma' | 'Jewellery';
type Costing =
  | 'None'
  | 'WeightedAverage'
  | 'Fifo'
  | 'Lifo'
  | 'Fefo'
  | 'SpecificIdentification';

interface Unit {
  uomId: number;
  uomTypeId: number;
  uomCode: string;
  uomName: string;
  isBaseUnit: boolean;
  conversionToBase: number;
  decimalPlaces: number;
}

interface UomType {
  uomTypeId: number;
  uomTypeName: string;
  units: Unit[];
}

interface TaxRate {
  taxGroupId: number;
  taxName: string;
  totalRate: number;
}

interface HsnCode {
  hsnSacCodeId: number;
  code: string;
  codeType: string;
  description: string;
  defaultGstRate: number | null;
}

interface HsnSearchResult {
  rows: HsnCode[];
}

interface Category {
  itemCategoryId: number;
  categoryName: string;
  defaultItemProfile: string | null;
  defaultCostingType: string | null;
  defaultUomTypeId: number | null;
  depth: number;
}

interface Purity {
  metalPurityId: number;
  metalType: string;
  purityName: string;
  purityFactor: number;
}

interface Warehouse {
  warehouseId: number;
  warehouseName: string;
  isDefault: boolean;
}

interface Barcode {
  itemBarcodeId: number;
  barcode: string;
  barcodeType: string;
  uomId: number | null;
  isPrimary: boolean;
  isActive: boolean;
}

interface ItemListRow {
  itemId: number;
  itemCode: string;
  itemName: string;
  itemProfile: Profile;
  itemType: string;
  categoryName: string | null;
  costingType: Costing;
  trackInventory: boolean;
  inventoryUomCode: string;
  salesPrice: number | null;
  mrp: number | null;
  isActive: boolean;
}

interface ItemDetail extends ItemListRow {
  printName: string | null;
  description: string | null;
  itemCategoryId: number | null;
  hsnSacCodeId: number | null;
  taxGroupId: number | null;
  taxPreference: string;
  isPriceInclusiveOfTax: boolean;
  uomTypeId: number;
  inventoryUomId: number;
  salesUomId: number;
  purchaseUomId: number;
  reportUomId: number;
  isBatchTracked: boolean;
  isExpiryTracked: boolean;
  isSerialTracked: boolean;
  purchasePrice: number | null;
  minSalePrice: number | null;
  standardCost: number | null;
  reorderLevel: number | null;
  reorderQuantity: number | null;
  minStockLevel: number | null;
  maxStockLevel: number | null;
  leadTimeDays: number | null;
  defaultWarehouseId: number | null;
  isSales: boolean;
  isPurchase: boolean;
  isReturnable: boolean;
  imageUrl: string | null;
  hasStockMovements: boolean;
  jewellery: Record<string, unknown> | null;
  pharma: Record<string, unknown> | null;
  barcodes: Barcode[];
}

type Tab = 'general' | 'units' | 'stock' | 'profile' | 'barcodes';

/**
 * The item master. One form serving three verticals: the profile decides which
 * extension tab appears, and the costing type decides which tracking options are
 * forced on and locked.
 *
 * Once stock has moved, the unit type, inventory unit, costing method and
 * tracking flags render read-only — every recorded quantity and cost was written
 * under them, so changing one would reinterpret history rather than correct it.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-items-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, NumberInputComponent, SearchInputComponent, DataGridComponent, DataGridCellTemplateDirective],
  templateUrl: './items.page.html',
  styleUrl: './items.page.scss',
})
export class ItemsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<ItemListRow[]>([]);
  protected readonly types = signal<UomType[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly taxRates = signal<TaxRate[]>([]);

  /** Matches for the HSN search box, and the code the item currently holds. */
  protected readonly hsnMatches = signal<HsnCode[]>([]);
  protected readonly hsnSelected = signal<HsnCode | null>(null);
  protected readonly hsnSearching = signal(false);
  protected readonly purities = signal<Purity[]>([]);
  protected readonly warehouses = signal<Warehouse[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
    tabFilter = 'itm';
  protected readonly messageIsError = signal(false);
  protected readonly editorOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly tab = signal<Tab>('general');

  search = '';
  profileFilter = '';
  showInactive = false;

  columns: ColumnDef[] = [
    { field: 'itemCode', header: 'Item code' },
    { field: 'itemName', header: 'Item name' },
    { field: 'hsnSac', header: 'HSN/SAC' },
    { field: 'status', header: 'Status' },
    { field: 'onHand', header: 'On hand', align: 'right' },
    { field: 'stockValue', header: 'Stock value', align: 'right' },
  ];

  form: ItemDetail = this.blank();
  hsnSearch = '';

  /** Units of the item's type — the only ones the four slots may point at. */
  protected readonly unitsOfType = computed(
    () => this.types().find((t) => t.uomTypeId === this.form.uomTypeId)?.units ?? [],
  );

  /** Quantity precision comes from the inventory unit, not the item. */
  protected readonly inventoryDecimals = computed(
    () => this.unitsOfType().find((u) => u.uomId === this.form.inventoryUomId)?.decimalPlaces ?? 0,
  );

  ngOnInit(): void {
    void this.load();
    void this.loadLookups();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = new URLSearchParams();
      if (this.search.trim()) {
        query.set('search', this.search.trim());
      }
      if (this.profileFilter) {
        query.set('profile', this.profileFilter);
      }
      query.set('includeInactive', String(this.showInactive));

      this.rows.set(await this.get<ItemListRow[]>(`/api/items?${query}`));
    } catch {
      this.fail('Could not load items.');
    } finally {
      this.busy.set(false);
    }
  }

  async loadLookups(): Promise<void> {
    const load = async <T>(url: string, set: (rows: T[]) => void): Promise<void> => {
      try {
        set(await this.get<T[]>(url));
      } catch {
        // A missing lookup disables one dropdown; it must not take the page down.
      }
    };

    await Promise.all([
      load<UomType>('/api/uom-types', (r) => this.types.set(r)),
      load<Category>('/api/item-categories', (r) => this.categories.set(r)),
      load<Purity>('/api/metal-purities', (r) => this.purities.set(r)),
      load<Warehouse>('/api/warehouses', (r) => this.warehouses.set(r)),
      load<TaxRate>('/api/tax-masters', (r) => this.taxRates.set(r)),
    ]);
  }

  openAdd(): void {
    this.editingId.set(null);
    this.form = this.blank();
    this.clearHsnSearch();
    this.tab.set('general');
    this.editorOpen.set(true);
  }

  async openEdit(row: ItemListRow): Promise<void> {
    this.busy.set(true);
    try {
      this.form = await this.get<ItemDetail>(`/api/items/${row.itemId}`);
      this.editingId.set(row.itemId);
      this.clearHsnSearch();
      void this.showCurrentHsn();
      this.tab.set('general');
      this.editorOpen.set(true);
    } catch {
      this.fail('Could not open that item.');
    } finally {
      this.busy.set(false);
    }
  }

  // ---- HSN / SAC ----------------------------------------------------------

  /**
   * The item stores only an id, and the search endpoint matches on code prefix,
   * so an id already held cannot be resolved by searching for it. Read the one
   * row instead, or the field shows blank on every saved item.
   */
  private async showCurrentHsn(): Promise<void> {
    if (this.form.hsnSacCodeId === null) {
      this.hsnSelected.set(null);
      return;
    }

    try {
      this.hsnSelected.set(
        await this.get<HsnCode>(`/api/master/hsn-sac/${this.form.hsnSacCodeId}`),
      );
    } catch {
      // A code that no longer resolves still has an id on the item. Showing
      // nothing would look like it was never set, so say what is stored.
      this.hsnSelected.set(null);
    }
  }

  async searchHsn(): Promise<void> {
    const term = this.hsnSearch.trim();
    if (term.length < 2) {
      this.hsnMatches.set([]);
      return;
    }

    this.hsnSearching.set(true);
    try {
      // Services take SAC and goods take HSN; filtering by the item's own type
      // keeps a chemist from being offered consulting codes.
      const codeType = this.form.itemType === 'Service' ? 'SAC' : 'HSN';
      const page = await this.get<HsnSearchResult>(
        `/api/master/hsn-sac?search=${encodeURIComponent(term)}&codeType=${codeType}&take=20`,
      );
      this.hsnMatches.set(page.rows);
    } catch {
      this.hsnMatches.set([]);
    } finally {
      this.hsnSearching.set(false);
    }
  }

  /**
   * Picking a code sets the item's HSN and, when the code carries a usual rate
   * and no rate has been chosen yet, pre-selects the matching one.
   *
   * **Pre-select, never overwrite.** The same code attracts a different rate by
   * condition, so a rate someone has already chosen is a decision, not a
   * default waiting to be corrected.
   */
  pickHsn(code: HsnCode): void {
    this.form.hsnSacCodeId = code.hsnSacCodeId;
    this.hsnSelected.set(code);
    this.hsnMatches.set([]);
    this.hsnSearch = '';

    if (code.defaultGstRate === null || this.form.taxGroupId !== null) {
      return;
    }

    const match = this.taxRates().find((r) => r.totalRate === code.defaultGstRate);
    if (match) {
      this.form.taxGroupId = match.taxGroupId;
    }
  }

  clearHsn(): void {
    this.form.hsnSacCodeId = null;
    this.hsnSelected.set(null);
    this.clearHsnSearch();
  }

  private clearHsnSearch(): void {
    this.hsnSearch = '';
    this.hsnMatches.set([]);
  }

  /** Choosing a category copies its defaults onto a new item, once. */
  onCategoryChange(): void {
    if (this.editingId() !== null) {
      return;
    }

    const category = this.categories().find((c) => c.itemCategoryId === this.form.itemCategoryId);
    if (!category) {
      return;
    }

    if (category.defaultItemProfile) {
      this.form.itemProfile = category.defaultItemProfile as Profile;
      this.onProfileChange();
    }

    if (category.defaultCostingType) {
      this.form.costingType = category.defaultCostingType as Costing;
      this.onCostingChange();
    }

    if (category.defaultUomTypeId) {
      this.form.uomTypeId = category.defaultUomTypeId;
      this.onUomTypeChange();
    }
  }

  /** All four unit slots must be of the chosen type, so switching resets them to its base. */
  onUomTypeChange(): void {
    const base = this.unitsOfType().find((u) => u.isBaseUnit) ?? this.unitsOfType()[0];
    const id = base?.uomId ?? 0;
    this.form.inventoryUomId = id;
    this.form.salesUomId = id;
    this.form.purchaseUomId = id;
    this.form.reportUomId = id;
  }

  onProfileChange(): void {
    if (this.form.itemProfile === 'Jewellery') {
      this.form.jewellery ??= {
        metalType: 'Gold',
        metalPurityId: this.purities()[0]?.metalPurityId ?? 0,
        grossWeight: 0,
        netWeight: 0,
        stoneWeight: 0,
        stoneCharge: 0,
        wastagePercent: 0,
        makingChargeType: 'Percentage',
        makingChargeValue: 0,
        isHallmarked: true,
      };
      this.form.pharma = null;
      this.form.costingType = 'SpecificIdentification';
      this.form.isSerialTracked = true;
    } else if (this.form.itemProfile === 'Pharma') {
      this.form.pharma ??= {
        genericName: '',
        strength: null,
        dosageForm: 'Tablet',
        packSize: '',
        manufacturerName: '',
        marketedBy: null,
        drugSchedule: 'None',
        isPrescriptionRequired: false,
        isNarcotic: false,
        storageCondition: 'Ambient',
        shelfLifeDays: null,
        minExpiryDaysOnReceipt: 180,
        expiryAlertDays: 90,
      };
      this.form.jewellery = null;
      this.form.costingType = 'Fefo';
      this.form.isBatchTracked = true;
      this.form.isExpiryTracked = true;
      this.form.isPriceInclusiveOfTax = true;
    } else {
      this.form.jewellery = null;
      this.form.pharma = null;
    }

    this.onCostingChange();
  }

  /** Costing forces the tracking it depends on; nothing else is touched. */
  onCostingChange(): void {
    if (this.form.costingType === 'Fefo') {
      this.form.isBatchTracked = true;
      this.form.isExpiryTracked = true;
    }

    if (this.form.costingType === 'SpecificIdentification') {
      this.form.isSerialTracked = true;
    }

    if (this.form.costingType === 'None') {
      this.form.trackInventory = false;
      this.form.isBatchTracked = false;
      this.form.isExpiryTracked = false;
      this.form.isSerialTracked = false;
    } else {
      this.form.trackInventory = true;
    }
  }

  onItemTypeChange(): void {
    if (this.form.itemType === 'Service') {
      this.form.costingType = 'None';
      this.onCostingChange();
    }
  }

  addBarcode(): void {
    this.form.barcodes = [
      ...this.form.barcodes,
      {
        itemBarcodeId: 0,
        barcode: '',
        barcodeType: this.form.itemProfile === 'Pharma' ? 'Gs1DataMatrix' : 'Ean13',
        uomId: this.form.inventoryUomId,
        isPrimary: this.form.barcodes.length === 0,
        isActive: true,
      },
    ];
  }

  removeBarcode(index: number): void {
    this.form.barcodes = this.form.barcodes.filter((_, i) => i !== index);
  }

  setPrimaryBarcode(index: number): void {
    this.form.barcodes = this.form.barcodes.map((b, i) => ({ ...b, isPrimary: i === index }));
  }

  async save(): Promise<void> {
    if (!this.validateForm()) {
      return;
    }

    const pharma = this.form.pharma as Record<string, unknown> | null;
    const jewellery = this.form.jewellery as Record<string, unknown> | null;
    const body = {
      ...this.form,
      itemCode: this.cleanOptional(this.form.itemCode),
      itemName: this.form.itemName.trim(),
      printName: this.cleanOptional(this.form.printName),
      description: this.cleanOptional(this.form.description),
      taxPreference: this.form.taxPreference.trim(),
      jewellery:
        jewellery === null
          ? null
          : {
              ...jewellery,
              metalType: this.asText(jewellery['metalType']),
            },
      pharma:
        pharma === null
          ? null
          : {
              ...pharma,
              genericName: this.asText(pharma['genericName']),
              strength: this.cleanOptional(this.asText(pharma['strength'])),
              packSize: this.asText(pharma['packSize']),
              manufacturerName: this.asText(pharma['manufacturerName']),
              marketedBy: this.cleanOptional(this.asText(pharma['marketedBy'])),
            },
      barcodes: this.form.barcodes.map((barcode) => ({
        ...barcode,
        barcode: barcode.barcode.trim(),
        barcodeType: barcode.barcodeType.trim(),
      })),
    };

    this.busy.set(true);
    try {
      const id = this.editingId();
      if (id === null) {
        await this.send('POST', '/api/items', body);
      } else {
        await this.send('PUT', `/api/items/${id}`, body);
      }
      this.editorOpen.set(false);
      this.succeed('Item saved.');
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'Could not save that item.');
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(row: ItemListRow): Promise<void> {
    this.busy.set(true);
    try {
      await this.send('DELETE', `/api/items/${row.itemId}`, {});
      this.succeed('Item deactivated.');
      await this.load();
    } catch {
      this.fail('Could not deactivate that item.');
    } finally {
      this.busy.set(false);
    }
  }

  /** The five fields frozen once stock has moved. */
  protected get locked(): boolean {
    return this.form.hasStockMovements;
  }

  protected get costingNote(): string {
    switch (this.form.costingType) {
      case 'None':
        return 'Not stocked — no cost is relieved when this is sold.';
      case 'WeightedAverage':
        return 'One average cost per item, across every warehouse in this branch.';
      case 'Fifo':
        return 'Cost layers consumed oldest receipt first.';
      case 'Lifo':
        return 'Newest receipt first. Not permitted for Indian statutory reporting.';
      case 'Fefo':
        return 'Layers consumed by earliest expiry — not earliest receipt.';
      case 'SpecificIdentification':
        return 'Each physical piece carries its own cost.';
      default:
        return '';
    }
  }

  private validateForm(): boolean {
    if (!this.hasText(this.form.itemName)) {
      this.fail('Item name is required.');
      return false;
    }

    if (
      !this.hasText(this.form.itemProfile) ||
      !this.hasText(this.form.itemType) ||
      !this.hasText(this.form.taxPreference) ||
      !this.hasText(this.form.costingType)
    ) {
      this.fail('Profile, type, tax preference and costing method are required.');
      return false;
    }

    if (
      this.form.uomTypeId <= 0 ||
      this.form.inventoryUomId <= 0 ||
      this.form.salesUomId <= 0 ||
      this.form.purchaseUomId <= 0 ||
      this.form.reportUomId <= 0
    ) {
      this.fail('Choose a unit type and all mandatory units.');
      return false;
    }

    if (this.form.itemProfile === 'Pharma') {
      const pharma = this.form.pharma as Record<string, unknown> | null;
      if (pharma === null) {
        this.fail('Pharma details are required for Pharma items.');
        return false;
      }

      if (!this.hasText(this.asText(pharma['genericName']))) {
        this.fail('Generic name is required for Pharma items.');
        return false;
      }

      if (!this.hasText(this.asText(pharma['packSize']))) {
        this.fail('Pack size is required for Pharma items.');
        return false;
      }

      if (!this.hasText(this.asText(pharma['manufacturerName']))) {
        this.fail('Manufacturer is required for Pharma items.');
        return false;
      }
    }

    if (this.form.itemProfile === 'Jewellery') {
      const jewellery = this.form.jewellery as Record<string, unknown> | null;
      if (jewellery === null) {
        this.fail('Jewellery details are required for Jewellery items.');
        return false;
      }

      if (!this.hasText(this.asText(jewellery['metalType']))) {
        this.fail('Metal type is required for Jewellery items.');
        return false;
      }

      if (!this.hasText(this.asText(jewellery['makingChargeType']))) {
        this.fail('Making charge type is required for Jewellery items.');
        return false;
      }

      const purityId = Number(jewellery['metalPurityId'] ?? 0);
      if (!Number.isFinite(purityId) || purityId <= 0) {
        this.fail('Choose a metal purity for Jewellery items.');
        return false;
      }
    }

    const blankBarcodeIndex = this.form.barcodes.findIndex((barcode) => !this.hasText(barcode.barcode));
    if (blankBarcodeIndex >= 0) {
      this.fail(`Barcode is required on row ${blankBarcodeIndex + 1}.`);
      return false;
    }

    const blankTypeIndex = this.form.barcodes.findIndex((barcode) => !this.hasText(barcode.barcodeType));
    if (blankTypeIndex >= 0) {
      this.fail(`Barcode type is required on row ${blankTypeIndex + 1}.`);
      return false;
    }

    return true;
  }

  private blank(): ItemDetail {
    const firstType = this.types()[0];
    const base = firstType?.units.find((u) => u.isBaseUnit) ?? firstType?.units[0];
    const uomId = base?.uomId ?? 0;

    return {
      itemId: 0,
      itemCode: '',
      itemName: '',
      itemProfile: 'Standard',
      itemType: 'Goods',
      categoryName: null,
      costingType: 'WeightedAverage',
      trackInventory: true,
      inventoryUomCode: '',
      salesPrice: null,
      mrp: null,
      isActive: true,
      printName: null,
      description: null,
      itemCategoryId: null,
      hsnSacCodeId: null,
      taxGroupId: null,
      taxPreference: 'Taxable',
      isPriceInclusiveOfTax: false,
      uomTypeId: firstType?.uomTypeId ?? 0,
      inventoryUomId: uomId,
      salesUomId: uomId,
      purchaseUomId: uomId,
      reportUomId: uomId,
      isBatchTracked: false,
      isExpiryTracked: false,
      isSerialTracked: false,
      purchasePrice: null,
      minSalePrice: null,
      standardCost: null,
      reorderLevel: null,
      reorderQuantity: null,
      minStockLevel: null,
      maxStockLevel: null,
      leadTimeDays: null,
      defaultWarehouseId: this.warehouses().find((w) => w.isDefault)?.warehouseId ?? null,
      isSales: true,
      isPurchase: true,
      isReturnable: true,
      imageUrl: null,
      hasStockMovements: false,
      jewellery: null,
      pharma: null,
      barcodes: [],
    };
  }

  private succeed(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private asText(value: unknown): string {
    return typeof value === 'string' ? value.trim() : '';
  }

  private cleanOptional(value: string | null | undefined): string | null {
    const cleaned = (value ?? '').trim();
    return cleaned.length > 0 ? cleaned : null;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}

