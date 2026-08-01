import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface StockPosition {
  itemId: number;
  itemCode: string;
  itemName: string;
  quantityOnHand: number;
  weightedAverageCost: number;
  stockValue: number;
  inventoryUomId: number;
  inventoryUomCode: string;
  reportUomId: number;
  reportUomCode: string;
  uomTypeId: number;
  quantityInReportUom: number;
  reorderLevel: number | null;
  isBelowReorderLevel: boolean;
  costingType: string;
  layeredStockValue: number;
  isBatchTracked: boolean;
  isExpiryTracked: boolean;
  isSerialTracked: boolean;
  usesCostLayers: boolean;
  lastMovementAt: string | null;
}

/** One layer an issue drew from — what it cost, and which receipt it came from. */
interface CostAllocation {
  costLayerId: number;
  receivedOn: string;
  expiresOn: string | null;
  batchNumber: string | null;
  quantity: number;
  unitCost: number;
  totalCost: number;
}

interface StockMovement {
  stockMovementId: number;
  itemId: number;
  itemCode: string;
  itemName: string;
  warehouseId: number | null;
  warehouseName: string | null;
  movementType: string;
  direction: 'In' | 'Out';
  movementDate: string;
  enteredQuantity: number;
  enteredUomCode: string;
  quantity: number;
  conversionFactor: number;
  unitCost: number | null;
  totalCost: number | null;
  resultingWeightedAverageCost: number | null;
  sourceType: string | null;
  sourceId: number | null;
  returnsStockMovementId: number | null;
  notes: string | null;
}

interface Unit {
  uomId: number;
  uomCode: string;
  uomName: string;
  uomTypeId: number;
  isActive: boolean;
}

/** Unit types are served with their units nested, so the list is flattened here. */
interface UomType {
  uomTypeId: number;
  units: Unit[];
}

interface Warehouse {
  warehouseId: number;
  warehouseName: string;
  isActive: boolean;
}

/** Movement types a person records by hand. The rest arrive from documents. */
const MANUAL_TYPES: readonly { value: string; label: string; needsCost: boolean }[] = [
  { value: 'Opening', label: 'Opening balance', needsCost: true },
  { value: 'Receipt', label: 'Receipt', needsCost: true },
  { value: 'Issue', label: 'Issue', needsCost: false },
  { value: 'Adjustment', label: 'Adjustment', needsCost: false },
  { value: 'SalesReturn', label: 'Sales return', needsCost: false },
];

/**
 * Inventory › Stock. One quantity and one cost per item, company-wide.
 *
 * There is deliberately no per-warehouse balance on this screen: stock is a
 * single shared pool, and a warehouse only says where a movement happened.
 */
@Component({
  selector: 'bb-stock-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './stock.page.html',
  styleUrl: './stock.page.scss',
})
export class StockPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<StockPosition[]>([]);
  protected readonly movements = signal<StockMovement[]>([]);
  protected readonly units = signal<Unit[]>([]);
  protected readonly warehouses = signal<Warehouse[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  /** The item whose ledger is open, or null for the position list. */
  protected readonly ledgerFor = signal<StockPosition | null>(null);
  protected readonly movementFor = signal<StockPosition | null>(null);

  protected readonly movementTypes = MANUAL_TYPES;
  protected readonly allocations = signal<CostAllocation[]>([]);

  /** Typed as one per line, so a scanner can be held down into the box. */
  serialText = '';
  hallmarkText = '';

  search = '';
  belowReorderOnly = false;

  form = this.blankMovement();

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
      query.set('belowReorderOnly', String(this.belowReorderOnly));

      this.rows.set(await this.get<StockPosition[]>(`/api/stock?${query}`));
    } catch {
      this.fail('Could not load stock.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadLookups(): Promise<void> {
    try {
      const types = await this.get<UomType[]>('/api/uom-types');
      this.units.set(types.flatMap((type) => type.units));
    } catch {
      /* the movement form falls back to the item's own inventory unit */
    }

    try {
      this.warehouses.set(await this.get<Warehouse[]>('/api/warehouses'));
    } catch {
      /* a movement without a warehouse is org-wide */
    }
  }

  async openLedger(row: StockPosition): Promise<void> {
    this.busy.set(true);
    try {
      this.movements.set(
        await this.get<StockMovement[]>(`/api/stock/movements?itemId=${row.itemId}`),
      );
      this.ledgerFor.set(row);
    } catch {
      this.fail('Could not load the movement history.');
    } finally {
      this.busy.set(false);
    }
  }

  openMovement(row: StockPosition): void {
    this.form = this.blankMovement();
    this.form.itemId = row.itemId;
    this.form.uomId = row.inventoryUomId;
    this.serialText = '';
    this.hallmarkText = '';
    this.movementFor.set(row);
  }

  /** Which layers an issue drew from. Empty on a weighted-average item. */
  async openAllocations(movement: StockMovement): Promise<void> {
    try {
      this.allocations.set(
        await this.get<CostAllocation[]>(
          `/api/stock/movements/${movement.stockMovementId}/allocations`,
        ),
      );
    } catch {
      this.fail('Could not load the cost allocations.');
    }
  }

  async record(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      // One per line, blanks dropped — a trailing newline should not become a
      // serial number the server then rejects.
      const lines = (text: string) =>
        text.split('\n').map((s) => s.trim()).filter((s) => s.length > 0);

      await this.send('POST', '/api/stock/movements', {
        ...this.form,
        serialNumbers: lines(this.serialText),
        hallmarkNumbers: lines(this.hallmarkText),
      });
      this.movementFor.set(null);
      this.message.set('Stock movement recorded.');
      this.messageIsError.set(false);
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'Could not record that movement.');
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Units the quantity may be entered in: every unit of the item's own type.
   * Anything else has no factor to convert through, and the server refuses it.
   */
  protected get enterableUnits(): Unit[] {
    const item = this.movementFor();
    return item === null
      ? []
      : this.units().filter((u) => u.uomTypeId === item.uomTypeId && u.isActive);
  }

  /** Only a purchase decides what stock cost; everything else is valued at the running average. */
  protected get costRequired(): boolean {
    return MANUAL_TYPES.find((t) => t.value === this.form.movementType)?.needsCost ?? false;
  }

  protected get directionEditable(): boolean {
    return this.form.movementType === 'Adjustment';
  }

  /** A return has to name the sale it reverses, or it cannot find the layers. */
  protected get isReturn(): boolean {
    return this.form.movementType === 'SalesReturn';
  }

  /** Issues of this item, newest first — what a return can point at. */
  protected readonly issues = signal<StockMovement[]>([]);

  async loadIssues(): Promise<void> {
    const item = this.movementFor();
    if (item === null || !this.isReturn) {
      return;
    }

    try {
      const all = await this.get<StockMovement[]>(
        `/api/stock/movements?itemId=${item.itemId}`,
      );
      this.issues.set(all.filter((m) => m.direction === 'Out'));
    } catch {
      /* the field stays empty and the server refuses an unnamed return */
    }
  }

  private blankMovement() {
    return {
      itemId: 0,
      movementType: 'Receipt',
      direction: 'In' as 'In' | 'Out',
      movementDate: new Date().toISOString().slice(0, 10),
      quantity: 0,
      uomId: null as number | null,
      warehouseId: null as number | null,
      unitCost: null as number | null,
      batchNumber: null as string | null,
      batchExpiryDate: null as string | null,
      batchMrp: null as number | null,
      returnsStockMovementId: null as number | null,
      notes: null as string | null,
    };
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
