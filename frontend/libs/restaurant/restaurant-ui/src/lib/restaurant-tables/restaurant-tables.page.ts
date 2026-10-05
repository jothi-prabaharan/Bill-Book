import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import {
  Floor,
  FloorPlan,
  RESTAURANT_API,
  RestaurantTable,
  TableStatus,
  floorSummary,
  mergeCandidates,
  moveTargets,
  nextStatuses,
  statusAction,
  statusLabel,
} from '@bill-book/restaurant-core';
import { BbSelectOption, CheckboxComponent, MessageBoxComponent, NumberInputComponent, SelectComponent, TextInputComponent, UiMessage } from '@bill-book/ui-components';

type Sheet = 'actions' | 'move' | 'merge' | 'edit-table' | 'edit-floor' | null;

/**
 * Restaurant › Tables (TK-145): the floor plan. Each table shows where it is
 * in its turn; tapping one offers what can happen next — seat, bill, clear,
 * move the party to another table, push tables together or pull them apart.
 * Floors and tables are added here too, by anyone allowed to create.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-tables',
  standalone: true,
  imports: [FormsModule, IfCanDirective, CheckboxComponent, MessageBoxComponent, NumberInputComponent, SelectComponent, TextInputComponent],
  templateUrl: './restaurant-tables.page.html',
  styleUrl: './restaurant-tables.page.scss',
})
export class RestaurantTablesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  protected readonly plan = signal<FloorPlan>({ floors: [] });
  protected readonly floorId = signal<number | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly managing = signal(false);

  protected readonly sheet = signal<Sheet>(null);
  protected readonly selected = signal<RestaurantTable | null>(null);
  protected readonly chosen = signal<Set<number>>(new Set());

  protected readonly floor = computed<Floor | null>(() => this.plan().floors.find((f) => f.floorId === this.floorId()) ?? this.plan().floors[0] ?? null);
  protected readonly floorOptions = computed<BbSelectOption<number>[]>(() => this.plan().floors.map((f) => ({ value: f.floorId, label: f.name })));
  protected readonly targets = computed(() => {
    const table = this.selected();
    if (!table) return [];
    return this.sheet() === 'merge' ? mergeCandidates(this.plan(), table) : moveTargets(this.plan(), table);
  });

  // Editing.
  protected editingTableId: number | null = null;
  protected tableDraft = { floorId: null as number | null, code: '', seats: 4 as number | null, isActive: true };
  protected editingFloorId: number | null = null;
  protected floorDraft = { name: '', isActive: true };

  protected readonly nextStatuses = nextStatuses;
  protected readonly statusAction = statusAction;
  protected readonly statusLabel = statusLabel;
  protected readonly floorSummary = floorSummary;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    try {
      const plan = await firstValueFrom(this.http.get<FloorPlan>(`${RESTAURANT_API}/floor-plan?includeInactive=${this.managing()}`));
      this.plan.set(plan);
      if (!plan.floors.some((f) => f.floorId === this.floorId())) {
        this.floorId.set(plan.floors[0]?.floorId ?? null);
      }
    } catch (error) {
      this.fail(error, 'The floor plan could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected toggleManaging(): void {
    this.managing.update((m) => !m);
    this.close();
    void this.load();
  }

  protected tap(table: RestaurantTable): void {
    this.selected.set(table);
    this.messages.set([]);
    if (this.managing()) {
      this.editTable(table);
    } else {
      this.sheet.set('actions');
    }
  }

  protected close(): void {
    this.sheet.set(null);
    this.selected.set(null);
    this.chosen.set(new Set());
  }

  /** To the order screen: the table's open order, or a new one at this table. */
  protected takeOrder(table: RestaurantTable): void {
    const headId = table.mergedIntoTableId ?? table.restaurantTableId;
    void this.router.navigate(['/restaurant/orders'], { queryParams: table.openOrderId ? { order: table.openOrderId } : { table: headId } });
  }

  protected async setStatus(table: RestaurantTable, status: TableStatus): Promise<void> {
    await this.run(() => firstValueFrom(this.http.put(`${RESTAURANT_API}/tables/${table.restaurantTableId}/status`, { status })), `Table ${table.code} is ${statusLabel(status).toLowerCase()}.`);
  }

  protected async moveTo(target: RestaurantTable): Promise<void> {
    const table = this.selected();
    if (!table) return;
    await this.run(
      () => firstValueFrom(this.http.post(`${RESTAURANT_API}/tables/${table.restaurantTableId}/move`, { toTableId: target.restaurantTableId })),
      `Moved from table ${table.code} to ${target.code}.`,
    );
  }

  protected toggleChosen(id: number, on: boolean): void {
    const next = new Set(this.chosen());
    if (on) next.add(id);
    else next.delete(id);
    this.chosen.set(next);
  }

  protected async merge(): Promise<void> {
    const table = this.selected();
    if (!table || !this.chosen().size) return;
    await this.run(
      () => firstValueFrom(this.http.post(`${RESTAURANT_API}/tables/${table.restaurantTableId}/merge`, { tableIds: [...this.chosen()] })),
      `Joined to table ${table.code}.`,
    );
  }

  protected async split(table: RestaurantTable): Promise<void> {
    await this.run(() => firstValueFrom(this.http.post(`${RESTAURANT_API}/tables/${table.restaurantTableId}/split`, {})), 'Tables separated.');
  }

  // ---- Managing ----

  protected newFloor(): void {
    this.editingFloorId = null;
    this.floorDraft = { name: '', isActive: true };
    this.sheet.set('edit-floor');
  }

  protected editFloor(floor: Floor): void {
    this.editingFloorId = floor.floorId;
    this.floorDraft = { name: floor.name, isActive: floor.isActive };
    this.sheet.set('edit-floor');
  }

  protected async saveFloor(): Promise<void> {
    if (!this.floorDraft.name.trim()) {
      this.messages.set([{ tone: 'error', text: "Enter the floor's name." }]);
      return;
    }
    const body = { name: this.floorDraft.name.trim(), isActive: this.floorDraft.isActive };
    await this.run(
      () =>
        this.editingFloorId == null
          ? firstValueFrom(this.http.post(`${RESTAURANT_API}/floors`, body))
          : firstValueFrom(this.http.put(`${RESTAURANT_API}/floors/${this.editingFloorId}`, body)),
      'Floor saved.',
    );
  }

  protected async deleteFloor(): Promise<void> {
    if (this.editingFloorId == null || !confirm('Delete this floor?')) return;
    const id = this.editingFloorId;
    await this.run(() => firstValueFrom(this.http.delete(`${RESTAURANT_API}/floors/${id}`)), 'Floor deleted.');
  }

  protected newTable(): void {
    this.editingTableId = null;
    this.tableDraft = { floorId: this.floor()?.floorId ?? null, code: '', seats: 4, isActive: true };
    this.selected.set(null);
    this.sheet.set('edit-table');
  }

  protected editTable(table: RestaurantTable): void {
    this.editingTableId = table.restaurantTableId;
    this.tableDraft = { floorId: table.floorId, code: table.code, seats: table.seats, isActive: table.isActive };
    this.sheet.set('edit-table');
  }

  protected async saveTable(): Promise<void> {
    const d = this.tableDraft;
    const problem = !d.floorId ? "Choose the table's floor." : !d.code.trim() ? "Enter the table's number or code." : !d.seats || d.seats < 1 || d.seats > 100 ? 'A table seats between 1 and 100.' : null;
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }
    const body = { floorId: d.floorId, code: d.code.trim(), seats: d.seats, isActive: d.isActive };
    await this.run(
      () =>
        this.editingTableId == null
          ? firstValueFrom(this.http.post(`${RESTAURANT_API}/tables`, body))
          : firstValueFrom(this.http.put(`${RESTAURANT_API}/tables/${this.editingTableId}`, body)),
      'Table saved.',
    );
  }

  protected async deleteTable(): Promise<void> {
    if (this.editingTableId == null || !confirm('Delete this table?')) return;
    const id = this.editingTableId;
    await this.run(() => firstValueFrom(this.http.delete(`${RESTAURANT_API}/tables/${id}`)), 'Table deleted.');
  }

  // ---- Shared ----

  private async run(work: () => Promise<unknown>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: done }]);
      this.close();
      await this.load();
    } catch (error) {
      this.fail(error, 'That could not be done.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
