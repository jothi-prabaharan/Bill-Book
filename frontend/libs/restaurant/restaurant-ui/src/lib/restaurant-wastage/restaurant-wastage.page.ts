import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import { RESTAURANT_WASTAGE_API, UnitOption, WASTAGE_REASONS, WastageEntry, WastageReason, wastageReasonLabel } from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  onCreateRequest,
} from '@bill-book/ui-components';

interface UomType {
  uomTypeId: number;
  units: UnitOption[];
}

interface ItemDetail {
  itemId: number;
  itemName: string;
  uomTypeId: number;
  inventoryUomId: number;
  trackInventory: boolean;
}

function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * Restaurant › Wastage (TK-149): food thrown away, and why. Each entry takes
 * the item out of stock through a stock adjustment, so it costs what it cost
 * and waits for approval where the branch approves adjustments.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-wastage',
  standalone: true,
  imports: [
    FormsModule,
    IfCanDirective,
    BranchDateInputComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    LookupDialogComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './restaurant-wastage.page.html',
  styleUrl: './restaurant-wastage.page.scss',
})
export class RestaurantWastagePage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly entries = signal<WastageEntry[]>([]);
  protected readonly units = signal<UnitOption[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly recording = signal(false);

  protected entryDate: string | null = today();
  protected readonly itemId = signal<number | null>(null);
  protected readonly itemLabel = signal('');
  protected readonly uomTypeId = signal<number | null>(null);
  protected quantity: number | null = null;
  protected uomId: number | null = null;
  protected reason: WastageReason = 'Spoiled';
  protected note = '';

  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  protected readonly reasonOptions: BbSelectOption<string>[] = WASTAGE_REASONS.map((r) => ({ value: r.value, label: r.label }));
  protected readonly unitOptions = computed<BbSelectOption<number>[]>(() =>
    this.units()
      .filter((u) => this.uomTypeId() === null || u.uomTypeId === this.uomTypeId())
      .map((u) => ({ value: u.uomId, label: `${u.uomCode} · ${u.uomName}` })),
  );
  protected readonly totalCost = computed(() => this.entries().reduce((sum, e) => sum + e.cost, 0));

  protected readonly columns: ColumnDef[] = [
    { field: 'entryDate', header: 'Date', isTemplate: true, width: '7rem' },
    { field: 'itemName', header: 'Item' },
    { field: 'quantity', header: 'Quantity', isTemplate: true, width: '8rem' },
    { field: 'reason', header: 'Why', isTemplate: true, width: '9rem' },
    { field: 'cost', header: 'Cost', isTemplate: true, width: '7rem' },
    { field: 'status', header: 'Status', width: '7rem' },
  ];

  protected readonly wastageReasonLabel = wastageReasonLabel;

  constructor() {
    // The secondary menu's + asks for a new wastage entry with ?action=create (TK-189).
    onCreateRequest(() => this.startRecording());
  }

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      const [entries, types] = await Promise.all([
        firstValueFrom(this.http.get<WastageEntry[]>(RESTAURANT_WASTAGE_API)),
        firstValueFrom(this.http.get<UomType[]>('/api/uom-types')),
      ]);
      this.entries.set(entries);
      this.units.set(types.flatMap((t) => t.units.map((u) => ({ ...u, uomTypeId: t.uomTypeId }))));
    } catch (error) {
      this.fail(error, 'The wastage could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected startRecording(): void {
    this.entryDate = today();
    this.itemId.set(null);
    this.itemLabel.set('');
    this.uomTypeId.set(null);
    this.quantity = null;
    this.uomId = null;
    this.reason = 'Spoiled';
    this.note = '';
    this.recording.set(true);
    this.messages.set([]);
  }

  protected openPicker(): void {
    this.pickerOpen.set(true);
    void this.search('');
  }

  protected async search(term: string): Promise<void> {
    this.pickerLoading.set(true);
    try {
      const query = new URLSearchParams({ includeInactive: 'false' });
      if (term.trim()) query.set('search', term.trim());
      const items = await firstValueFrom(this.http.get<{ itemId: number; itemCode: string; itemName: string }[]>(`/api/items?${query}`));
      this.pickerRows.set(items.map((i) => ({ id: i.itemId, code: i.itemCode, name: i.itemName })));
    } catch {
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected async pick(row: LookupRow): Promise<void> {
    this.pickerOpen.set(false);
    try {
      const item = await firstValueFrom(this.http.get<ItemDetail>(`/api/items/${row.id}`));
      if (!item.trackInventory) {
        this.messages.set([{ tone: 'error', text: `${item.itemName} is not a stocked item, so there is no stock to write off.` }]);
        return;
      }
      this.itemId.set(item.itemId);
      this.itemLabel.set(item.itemName);
      this.uomTypeId.set(item.uomTypeId);
      this.uomId = item.inventoryUomId;
    } catch (error) {
      this.fail(error, 'The item could not be read.');
    }
  }

  protected async record(): Promise<void> {
    const problem = !this.itemId() ? 'Choose the item thrown away.' : !this.quantity || this.quantity <= 0 ? 'Enter how much was thrown away.' : null;
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(
        this.http.post(RESTAURANT_WASTAGE_API, {
          entryDate: this.entryDate,
          itemId: this.itemId(),
          quantity: this.quantity,
          uomId: this.uomId,
          reason: this.reason,
          note: this.note.trim() || null,
        }),
      );
      this.recording.set(false);
      this.entries.set(await firstValueFrom(this.http.get<WastageEntry[]>(RESTAURANT_WASTAGE_API)));
      const latest = this.entries()[0];
      this.messages.set([
        latest?.status === 'Draft'
          ? { tone: 'info', text: 'Recorded. The write-off waits for approval before it leaves stock.' }
          : { tone: 'success', text: 'Recorded and taken out of stock.' },
      ]);
    } catch (error) {
      this.fail(error, 'The wastage could not be recorded.');
    } finally {
      this.busy.set(false);
    }
  }

  protected money(value: number): string {
    return this.formats.formatMoney(value);
  }

  protected date(value: string): string {
    return this.formats.formatDate(value);
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
