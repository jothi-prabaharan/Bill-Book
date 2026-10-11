import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  EVERY_DAY,
  GroupDraft,
  MENU_DAYS,
  MenuCategory,
  MenuItem,
  MenuItemDraft,
  MenuSchedule,
  ORDER_TYPES,
  RESTAURANT_MENU_API,
  STATIONS,
  ScheduleDraft,
  WEEKDAYS,
  WEEKEND,
  daysLabel,
  emptyGroup,
  emptyMenuItem,
  emptyModifier,
  emptyVariant,
  halfAndFull,
  hoursLabel,
  menuItemDraftOf,
  menuItemProblem,
  menuItemRequest,
  orderTypeLabel,
  priceSummary,
  scheduleDraftOf,
  scheduleProblem,
  scheduleRequest,
} from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  onCreateRequest,
} from '@bill-book/ui-components';

type Tab = 'dishes' | 'categories' | 'schedules';

interface ItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
  salesPrice: number | null;
}

/**
 * Restaurant › Menu (TK-144). A dish is an item from the item master; here it
 * is put in a category, given portions (Half, Full), choices (spice level,
 * extras), prices that differ by order type, a kitchen station and a timed
 * menu. "Sold out" is one tap in the list, so it works from a phone at the pass.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-menu',
  standalone: true,
  imports: [
    FormsModule,
    IfCanDirective,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    LookupDialogComponent,
    MessageBoxComponent,
    MoneyInputComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './restaurant-menu.page.html',
  styleUrl: './restaurant-menu.page.scss',
})
export class RestaurantMenuPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly tab = signal<Tab>('dishes');
  protected readonly tabs: { tab: Tab; label: string }[] = [
    { tab: 'dishes', label: 'Dishes' },
    { tab: 'categories', label: 'Categories' },
    { tab: 'schedules', label: 'Timed menus' },
  ];

  protected readonly items = signal<MenuItem[]>([]);
  protected readonly categories = signal<MenuCategory[]>([]);
  protected readonly schedules = signal<MenuSchedule[]>([]);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  // ---- Dishes ----
  protected categoryFilter: number | null = null;
  protected search = '';
  protected readonly editing = signal<MenuItem | 'new' | null>(null);
  protected readonly itemLabel = signal('');
  protected draft: MenuItemDraft = emptyMenuItem();

  protected readonly columns: ColumnDef[] = [
    { field: 'itemName', header: 'Dish' },
    { field: 'categoryName', header: 'Category', width: '9rem' },
    { field: 'price', header: 'Price', isTemplate: true, width: '12rem' },
    { field: 'station', header: 'Station', width: '6rem' },
    { field: 'isAvailable', header: 'Available', isTemplate: true, width: '7rem' },
  ];

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.menuCategoryId, label: c.isActive ? c.name : `${c.name} (inactive)` })),
  );
  protected readonly scheduleOptions = computed<BbSelectOption<number>[]>(() =>
    this.schedules().map((s) => ({ value: s.menuScheduleId, label: `${s.name} · ${daysLabel(s.days)} ${hoursLabel(s.startTime, s.endTime)}` })),
  );
  protected readonly stationOptions: BbSelectOption<string>[] = STATIONS.map((s) => ({ value: s.value, label: s.label }));
  protected readonly orderTypeOptions: BbSelectOption<string>[] = ORDER_TYPES.map((o) => ({ value: o.value, label: o.label }));

  // Item picker.
  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);
  private pickerItems: ItemOption[] = [];

  // ---- Categories ----
  protected readonly editingCategory = signal<MenuCategory | 'new' | null>(null);
  protected categoryName = '';
  protected categoryActive = true;

  // ---- Timed menus ----
  protected readonly editingSchedule = signal<MenuSchedule | 'new' | null>(null);
  protected scheduleDraft: ScheduleDraft = { name: '', days: EVERY_DAY, startTime: '07:00', endTime: '11:00', isActive: true };
  protected readonly days = MENU_DAYS;
  protected readonly everyDay = EVERY_DAY;
  protected readonly weekdays = WEEKDAYS;
  protected readonly weekend = WEEKEND;

  protected readonly daysLabel = daysLabel;
  protected readonly hoursLabel = hoursLabel;
  protected readonly orderTypeLabel = orderTypeLabel;

  constructor() {
    // The secondary menu's + asks for a new dish with ?action=create (TK-189). The dish's
    // sheet is on the Dishes tab, and like "Add a dish" it waits for a category to go under.
    onCreateRequest(
      () => {
        this.choose('dishes');
        this.newDish();
      },
      () => this.categories().length > 0,
    );
  }

  ngOnInit(): void {
    void this.loadAll();
  }

  private async loadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const [categories, schedules] = await Promise.all([
        firstValueFrom(this.http.get<MenuCategory[]>(`${RESTAURANT_MENU_API}/categories?includeInactive=true`)),
        firstValueFrom(this.http.get<MenuSchedule[]>(`${RESTAURANT_MENU_API}/schedules`)),
      ]);
      this.categories.set(categories);
      this.schedules.set(schedules);
      await this.loadItems();
    } catch (error) {
      this.fail(error, 'The menu could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected async loadItems(): Promise<void> {
    const query = new URLSearchParams();
    if (this.categoryFilter) query.set('categoryId', String(this.categoryFilter));
    if (this.search.trim()) query.set('search', this.search.trim());
    try {
      this.items.set(await firstValueFrom(this.http.get<MenuItem[]>(`${RESTAURANT_MENU_API}/items?${query}`)));
    } catch (error) {
      this.fail(error, 'The dishes could not be loaded.');
    }
  }

  protected choose(tab: Tab): void {
    this.tab.set(tab);
    this.messages.set([]);
  }

  protected price(item: MenuItem): string {
    return priceSummary(item, (v) => this.formats.formatMoney(v));
  }

  // ---- Dishes ----

  protected newDish(): void {
    this.draft = emptyMenuItem(this.categoryFilter ?? this.categories().find((c) => c.isActive)?.menuCategoryId ?? null);
    this.itemLabel.set('');
    this.editing.set('new');
    this.messages.set([]);
  }

  protected openDish(item: MenuItem): void {
    this.draft = menuItemDraftOf(item);
    this.itemLabel.set(`${item.itemCode} · ${item.itemName}`);
    this.editing.set(item);
    this.messages.set([]);
  }

  protected closeDish(): void {
    this.editing.set(null);
  }

  protected async toggleAvailable(item: MenuItem, isAvailable: boolean): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.http.put(`${RESTAURANT_MENU_API}/items/${item.itemId}/availability`, { isAvailable }));
      this.items.update((rows) => rows.map((r) => (r.itemId === item.itemId ? { ...r, isAvailable } : r)));
    }, isAvailable ? `${item.itemName} is back on.` : `${item.itemName} is sold out.`, false);
  }

  protected addVariant(): void {
    this.draft.variants = [...this.draft.variants, emptyVariant()];
  }

  protected addHalfAndFull(): void {
    const editing = this.editing();
    const base = editing && editing !== 'new' ? editing.basePrice : null;
    this.draft.variants = halfAndFull(base);
    // A dish sold by portion has no dish-level order-type price.
    this.draft.prices = this.draft.prices.filter((p) => p.variantName.trim());
  }

  protected removeVariant(index: number): void {
    const name = this.draft.variants[index]?.name.trim().toLowerCase();
    this.draft.variants = this.draft.variants.filter((_, i) => i !== index);
    this.draft.prices = this.draft.prices.filter((p) => p.variantName.trim().toLowerCase() !== name);
  }

  protected variantOptions(): BbSelectOption<string>[] {
    return this.draft.variants.filter((v) => v.name.trim()).map((v) => ({ value: v.name.trim(), label: v.name.trim() }));
  }

  protected addPrice(): void {
    const firstVariant = this.draft.variants.find((v) => v.name.trim())?.name.trim() ?? '';
    this.draft.prices = [...this.draft.prices, { orderType: 'Takeaway', variantName: firstVariant, price: null }];
  }

  protected removePrice(index: number): void {
    this.draft.prices = this.draft.prices.filter((_, i) => i !== index);
  }

  protected addGroup(): void {
    this.draft.groups = [...this.draft.groups, emptyGroup()];
  }

  protected removeGroup(index: number): void {
    this.draft.groups = this.draft.groups.filter((_, i) => i !== index);
  }

  protected addModifier(group: GroupDraft): void {
    group.modifiers = [...group.modifiers, emptyModifier()];
  }

  protected removeModifier(group: GroupDraft, index: number): void {
    group.modifiers = group.modifiers.filter((_, i) => i !== index);
  }

  protected async saveDish(): Promise<void> {
    const problem = menuItemProblem(this.draft);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    const editing = this.editing();
    await this.run(async () => {
      const body = menuItemRequest(this.draft);
      if (editing && editing !== 'new') {
        await firstValueFrom(this.http.put(`${RESTAURANT_MENU_API}/items/${editing.itemId}`, body));
      } else {
        await firstValueFrom(this.http.post(`${RESTAURANT_MENU_API}/items`, body));
      }
      this.editing.set(null);
      await this.loadItems();
    }, 'Dish saved.');
  }

  protected async removeDish(): Promise<void> {
    const editing = this.editing();
    if (!editing || editing === 'new') return;
    const ok = await this.confirmDialog.confirm({
      title: 'Take dish off menu?',
      message: `Take ${editing.itemName} off the menu? The item itself stays in the item master.`,
      confirmLabel: 'Take off menu',
      tone: 'danger',
    });
    if (!ok) return;
    await this.run(async () => {
      await firstValueFrom(this.http.delete(`${RESTAURANT_MENU_API}/items/${editing.itemId}`));
      this.editing.set(null);
      await this.loadItems();
    }, 'Dish taken off the menu.');
  }

  protected openPicker(): void {
    this.pickerOpen.set(true);
    void this.searchItems('');
  }

  protected async searchItems(term: string): Promise<void> {
    this.pickerLoading.set(true);
    try {
      const query = new URLSearchParams({ includeInactive: 'false' });
      if (term.trim()) query.set('search', term.trim());
      this.pickerItems = await firstValueFrom(this.http.get<ItemOption[]>(`/api/items?${query}`));
      const onMenu = new Set(this.items().map((i) => i.itemId));
      this.pickerRows.set(
        this.pickerItems
          .filter((i) => !onMenu.has(i.itemId))
          .map((i) => ({ id: i.itemId, code: i.itemCode, name: i.itemName, meta: i.salesPrice == null ? 'No sales price' : this.formats.formatMoney(i.salesPrice) })),
      );
    } catch {
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected pickItem(row: LookupRow): void {
    this.draft.itemId = row.id;
    this.itemLabel.set(`${row.code} · ${row.name}`);
    this.pickerOpen.set(false);
  }

  // ---- Categories ----

  protected newCategory(): void {
    this.categoryName = '';
    this.categoryActive = true;
    this.editingCategory.set('new');
  }

  protected openCategory(category: MenuCategory): void {
    this.categoryName = category.name;
    this.categoryActive = category.isActive;
    this.editingCategory.set(category);
  }

  protected async saveCategory(): Promise<void> {
    if (!this.categoryName.trim()) {
      this.messages.set([{ tone: 'error', text: "Enter the category's name." }]);
      return;
    }

    const editing = this.editingCategory();
    await this.run(async () => {
      const body = { name: this.categoryName.trim(), isActive: this.categoryActive };
      if (editing && editing !== 'new') {
        await firstValueFrom(this.http.put(`${RESTAURANT_MENU_API}/categories/${editing.menuCategoryId}`, body));
      } else {
        await firstValueFrom(this.http.post(`${RESTAURANT_MENU_API}/categories`, body));
      }
      this.editingCategory.set(null);
      this.categories.set(await firstValueFrom(this.http.get<MenuCategory[]>(`${RESTAURANT_MENU_API}/categories?includeInactive=true`)));
    }, 'Category saved.');
  }

  protected async deleteCategory(category: MenuCategory): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete category?',
      message: `Delete the category ${category.name}?`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    await this.run(async () => {
      await firstValueFrom(this.http.delete(`${RESTAURANT_MENU_API}/categories/${category.menuCategoryId}`));
      this.editingCategory.set(null);
      this.categories.update((rows) => rows.filter((r) => r.menuCategoryId !== category.menuCategoryId));
    }, 'Category deleted.');
  }

  // ---- Timed menus ----

  protected newSchedule(): void {
    this.scheduleDraft = { name: '', days: EVERY_DAY, startTime: '07:00', endTime: '11:00', isActive: true };
    this.editingSchedule.set('new');
  }

  protected openSchedule(schedule: MenuSchedule): void {
    this.scheduleDraft = scheduleDraftOf(schedule);
    this.editingSchedule.set(schedule);
  }

  protected hasDay(flag: number): boolean {
    return (this.scheduleDraft.days & flag) !== 0;
  }

  protected toggleDay(flag: number, on: boolean): void {
    this.scheduleDraft = { ...this.scheduleDraft, days: on ? this.scheduleDraft.days | flag : this.scheduleDraft.days & ~flag };
  }

  protected setDays(days: number): void {
    this.scheduleDraft = { ...this.scheduleDraft, days };
  }

  protected async saveSchedule(): Promise<void> {
    const problem = scheduleProblem(this.scheduleDraft);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    const editing = this.editingSchedule();
    await this.run(async () => {
      const body = scheduleRequest(this.scheduleDraft);
      if (editing && editing !== 'new') {
        await firstValueFrom(this.http.put(`${RESTAURANT_MENU_API}/schedules/${editing.menuScheduleId}`, body));
      } else {
        await firstValueFrom(this.http.post(`${RESTAURANT_MENU_API}/schedules`, body));
      }
      this.editingSchedule.set(null);
      this.schedules.set(await firstValueFrom(this.http.get<MenuSchedule[]>(`${RESTAURANT_MENU_API}/schedules`)));
    }, 'Timed menu saved.');
  }

  protected async deleteSchedule(schedule: MenuSchedule): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete timed menu?',
      message: `Delete the timed menu ${schedule.name}? Its dishes will be served all day.`,
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;
    await this.run(async () => {
      await firstValueFrom(this.http.delete(`${RESTAURANT_MENU_API}/schedules/${schedule.menuScheduleId}`));
      this.editingSchedule.set(null);
      this.schedules.update((rows) => rows.filter((r) => r.menuScheduleId !== schedule.menuScheduleId));
      await this.loadItems();
    }, 'Timed menu deleted.');
  }

  // ---- Shared ----

  private async run(work: () => Promise<void>, done: string, showDone = true): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set(showDone ? [{ tone: 'success', text: done }] : [{ tone: 'info', text: done }]);
    } catch (error) {
      this.fail(error, 'That could not be saved.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
