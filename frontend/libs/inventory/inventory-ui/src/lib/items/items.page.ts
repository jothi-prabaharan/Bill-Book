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
@Component({
  selector: 'bb-items-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './items.page.html',
  styleUrl: './items.page.scss',
})
export class ItemsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<ItemListRow[]>([]);
  protected readonly types = signal<UomType[]>([]);
  protected readonly categories = signal<Category[]>([]);
  protected readonly purities = signal<Purity[]>([]);
  protected readonly warehouses = signal<Warehouse[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editorOpen = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly tab = signal<Tab>('general');

  search = '';
  profileFilter = '';
  showInactive = false;

  form: ItemDetail = this.blank();

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
    ]);
  }

  openAdd(): void {
    this.editingId.set(null);
    this.form = this.blank();
    this.tab.set('general');
    this.editorOpen.set(true);
  }

  async openEdit(row: ItemListRow): Promise<void> {
    this.busy.set(true);
    try {
      this.form = await this.get<ItemDetail>(`/api/items/${row.itemId}`);
      this.editingId.set(row.itemId);
      this.tab.set('general');
      this.editorOpen.set(true);
    } catch {
      this.fail('Could not open that item.');
    } finally {
      this.busy.set(false);
    }
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
    this.busy.set(true);
    try {
      const id = this.editingId();
      if (id === null) {
        await this.send('POST', '/api/items', this.form);
      } else {
        await this.send('PUT', `/api/items/${id}`, this.form);
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
        return 'One average cost per item, company-wide across every branch.';
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
