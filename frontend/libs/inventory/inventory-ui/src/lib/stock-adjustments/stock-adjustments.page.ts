import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef , DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface AdjustmentLine {
  stockAdjustmentLineId: number;
  lineNumber: number;
  itemId: number;
  itemCode: string;
  itemName: string;
  uomId: number | null;
  uomCode: string | null;
  inventoryUomCode: string;
  systemQuantity: number | null;
  countedQuantity: number | null;
  quantity: number;
  direction: 'In' | 'Out';
  unitCost: number | null;
  batchNumber: string | null;
  batchExpiryDate: string | null;
  stockMovementId: number | null;
  movementTotalCost: number | null;
  notes: string | null;
}

interface Adjustment {
  stockAdjustmentId: number;
  adjustmentNo: string | null;
  adjustmentDate: string;
  kind: 'Adjustment' | 'PhysicalCount';
  reason: string;
  status: 'Draft' | 'Posted' | 'Reversed';
  warehouseId: number | null;
  warehouseName: string | null;
  lineCount: number;
  /** Null while any line is still uncosted — a partial total reads like a complete one. */
  netValue: number | null;
  notes: string | null;
  reversedByStockAdjustmentId: number | null;
  reversesStockAdjustmentId: number | null;
  postedAt: string | null;
  lines?: AdjustmentLine[];
}

interface Item {
  itemId: number;
  itemCode: string;
  itemName: string;
  inventoryUomId: number;
}

interface Warehouse {
  warehouseId: number;
  warehouseName: string;
  isActive: boolean;
}

/** What is being keyed on a line, before the server works out the difference. */
interface DraftLine {
  itemId: number | null;
  countedQuantity: number | null;
  quantity: number | null;
  direction: 'In' | 'Out';
  unitCost: number | null;
  batchNumber: string | null;
  batchExpiryDate: string | null;
  notes: string | null;
}

const REASONS: readonly { value: string; label: string }[] = [
  { value: 'CountCorrection', label: 'Count correction' },
  { value: 'Damage', label: 'Damaged' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Theft', label: 'Theft' },
  { value: 'Sample', label: 'Sample or giveaway' },
  { value: 'InternalUse', label: 'Used internally' },
  { value: 'FoundStock', label: 'Found stock' },
  { value: 'Other', label: 'Other' },
];

/**
 * Inventory › Stock adjustments. A write-off or a physical count, posted as one
 * document rather than as loose movements.
 *
 * There is no void: a posted sheet has moved stock that physically moved, so the
 * correction is a mirror sheet.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-stock-adjustments-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './stock-adjustments.page.html',
  styleUrl: './stock-adjustments.page.scss',
})
export class StockAdjustmentsPage implements OnInit {

  columns: ColumnDef[] = [
    { field: 'date', header: 'Date' },
    { field: 'reason', header: 'Reason' },
    { field: 'item', header: 'Item' },
    { field: 'quantity', header: 'Adjusted by', align: 'right' },
    { field: 'value', header: 'Value', align: 'right' },
    { field: 'actions', header: '' }
  ];

  private readonly http = inject(HttpClient);

  protected readonly rows = signal<Adjustment[]>([]);
  protected readonly items = signal<Item[]>([]);
  protected readonly warehouses = signal<Warehouse[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  /** The sheet being keyed, or null when the list is showing. */
  protected readonly editing = signal<Adjustment | null>(null);
  protected readonly viewing = signal<Adjustment | null>(null);

  protected readonly reasons = REASONS;

  form = this.blank();
  lines: DraftLine[] = [];

  listColumns: ColumnDef[] = [
    { field: 'adjustmentNo', header: 'Number' },
    { field: 'adjustmentDate', header: 'Date' },
    { field: 'kind', header: 'Kind' },
    { field: 'reason', header: 'Reason' },
    { field: 'lineCount', header: 'Lines', dataType: 'number' },
    { field: 'netValue', header: 'Net value', dataType: 'number' },
    { field: 'status', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  get draftColumns(): ColumnDef[] {
    const cols: ColumnDef[] = [
      { field: 'item', header: 'Item' }
    ];
    if (this.isCount) {
      cols.push({ field: 'countedQuantity', header: 'Counted', dataType: 'number' });
    } else {
      cols.push(
        { field: 'quantity', header: 'Quantity', dataType: 'number' },
        { field: 'direction', header: 'Direction' }
      );
    }
    cols.push(
      { field: 'unitCost', header: 'Cost per unit', dataType: 'number' },
      { field: 'batchNumber', header: 'Batch' },
      { field: 'batchExpiryDate', header: 'Expiry' },
      { field: 'notes', header: 'Note' },
      { field: 'actions', header: '' }
    );
    return cols;
  }

  get viewColumns(): ColumnDef[] {
    const isCount = this.viewing()?.kind === 'PhysicalCount';
    const cols: ColumnDef[] = [
      { field: 'lineNumber', header: '#' },
      { field: 'item', header: 'Item' }
    ];
    if (isCount) {
      cols.push(
        { field: 'systemQuantity', header: 'System held', dataType: 'number' },
        { field: 'countedQuantity', header: 'Counted', dataType: 'number' }
      );
    }
    cols.push(
      { field: 'movedQuantity', header: 'Moved', dataType: 'number' },
      { field: 'movementTotalCost', header: 'Cost', dataType: 'number' },
      { field: 'notes', header: 'Note' }
    );
    return cols;
  }

  ngOnInit(): void {
    void this.load();
    void this.loadLookups();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<Adjustment[]>('/api/stock-adjustments'));
    } catch {
      this.fail('Could not load adjustments.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadLookups(): Promise<void> {
    try {
      this.items.set(await this.get<Item[]>('/api/items'));
    } catch {
      /* the item picker stays empty and nothing can be keyed */
    }

    try {
      this.warehouses.set(await this.get<Warehouse[]>('/api/warehouses'));
    } catch {
      /* a sheet without a warehouse is branch-wide, which is legal */
    }
  }

  protected get isCount(): boolean {
    return this.form.kind === 'PhysicalCount';
  }

  newSheet(kind: 'Adjustment' | 'PhysicalCount'): void {
    this.form = this.blank();
    this.form.kind = kind;
    this.form.reason = kind === 'PhysicalCount' ? 'CountCorrection' : 'Damage';
    this.lines = [this.blankLine()];
    this.editing.set({ stockAdjustmentId: 0 } as Adjustment);
  }

  async edit(row: Adjustment): Promise<void> {
    const full = await this.get<Adjustment>(`/api/stock-adjustments/${row.stockAdjustmentId}`);

    this.form = {
      kind: full.kind,
      reason: full.reason,
      adjustmentDate: full.adjustmentDate,
      warehouseId: full.warehouseId,
      notes: full.notes,
    };

    this.lines = (full.lines ?? []).map((l) => ({
      itemId: l.itemId,
      countedQuantity: l.countedQuantity,
      quantity: l.quantity,
      direction: l.direction,
      unitCost: l.unitCost,
      batchNumber: l.batchNumber,
      batchExpiryDate: l.batchExpiryDate,
      notes: l.notes,
    }));

    if (this.lines.length === 0) {
      this.lines = [this.blankLine()];
    }

    this.editing.set(full);
  }

  async view(row: Adjustment): Promise<void> {
    this.viewing.set(
      await this.get<Adjustment>(`/api/stock-adjustments/${row.stockAdjustmentId}`),
    );
  }

  addLine(): void {
    this.lines = [...this.lines, this.blankLine()];
  }

  removeLine(index: number): void {
    this.lines = this.lines.filter((_, i) => i !== index);
  }

  /** Lines with no item chosen are not sent; a blank row at the foot is normal. */
  private get keyedLines(): DraftLine[] {
    return this.lines.filter((l) => l.itemId !== null);
  }

  protected get canSave(): boolean {
    return this.keyedLines.length > 0;
  }

  async save(): Promise<void> {
    const current = this.editing();
    if (current === null) {
      return;
    }

    this.busy.set(true);
    this.message.set(null);

    try {
      const body = {
        ...this.form,
        lines: this.keyedLines.map((l) => ({
          itemId: l.itemId,
          // The server ignores the half that does not apply, but sending only
          // what was keyed keeps the request honest about what was meant.
          countedQuantity: this.isCount ? l.countedQuantity : null,
          quantity: this.isCount ? null : l.quantity,
          direction: this.isCount ? null : l.direction,
          unitCost: l.direction === 'In' ? l.unitCost : null,
          batchNumber: l.batchNumber,
          batchExpiryDate: l.batchExpiryDate,
          notes: l.notes,
        })),
      };

      const saved =
        current.stockAdjustmentId > 0
          ? await this.send<{ stockAdjustmentId: number }>(
              'PUT',
              `/api/stock-adjustments/${current.stockAdjustmentId}`,
              body,
            )
          : await this.send<{ stockAdjustmentId: number }>(
              'POST',
              '/api/stock-adjustments',
              body,
            );

      this.editing.set(null);
      this.notify('Saved as a draft. Nothing has moved yet.');
      await this.load();
      void saved;
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not save the sheet.'));
    } finally {
      this.busy.set(false);
    }
  }

  async post(row: Adjustment): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      await this.send('POST', `/api/stock-adjustments/${row.stockAdjustmentId}/post`, {});
      this.notify('Posted. The stock has moved and the sheet has its number.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not post the sheet.'));
    } finally {
      this.busy.set(false);
    }
  }

  async reverse(row: Adjustment): Promise<void> {
    this.busy.set(true);
    this.message.set(null);

    try {
      await this.send('POST', `/api/stock-adjustments/${row.stockAdjustmentId}/reverse`, {});
      this.notify('Reversed by a mirror sheet. Both documents keep their numbers.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not reverse the sheet.'));
    } finally {
      this.busy.set(false);
    }
  }

  async remove(row: Adjustment): Promise<void> {
    this.busy.set(true);

    try {
      await this.send('DELETE', `/api/stock-adjustments/${row.stockAdjustmentId}`, {});
      this.notify('Draft discarded.');
      await this.load();
    } catch (err: unknown) {
      this.fail(this.reason(err, 'Could not discard the draft.'));
    } finally {
      this.busy.set(false);
    }
  }

  protected labelFor(reason: string): string {
    return REASONS.find((r) => r.value === reason)?.label ?? reason;
  }

  private blank() {
    return {
      kind: 'Adjustment' as 'Adjustment' | 'PhysicalCount',
      reason: 'Damage',
      adjustmentDate: new Date().toISOString().slice(0, 10),
      warehouseId: null as number | null,
      notes: null as string | null,
    };
  }

  private blankLine(): DraftLine {
    return {
      itemId: null,
      countedQuantity: null,
      quantity: null,
      direction: 'Out',
      unitCost: null,
      batchNumber: null,
      batchExpiryDate: null,
      notes: null,
    };
  }

  private reason(err: unknown, fallback: string): string {
    const anyErr = err as { error?: { message?: string } };
    return anyErr?.error?.message ?? fallback;
  }

  private notify(text: string): void {
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

