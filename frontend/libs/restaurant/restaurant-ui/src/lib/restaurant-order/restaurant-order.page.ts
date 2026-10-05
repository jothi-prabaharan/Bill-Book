import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService, IfCanDirective } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  FloorPlan,
  Kot,
  KotLine,
  ORDER_TYPES,
  PrintedDocument,
  canCancel,
  cancelNeedsApproval,
  OrderType,
  OrderingCategory,
  OrderingItem,
  OrderingMenu,
  OrderingVariant,
  PendingLine,
  RESTAURANT_API,
  RESTAURANT_MENU_API,
  RESTAURANT_ORDERS_API,
  RestaurantOrder,
  RestaurantOrderLine,
  RestaurantSettingsService,
  RestaurantSettingsView,
  StaffMember,
  addsInOneTap,
  allTables,
  choiceProblem,
  defaultRestaurantSettings,
  kitchenStatusLabel,
  linesRequest,
  orderPlace,
  orderProblem,
  pendingPrice,
  pendingTotal,
  requiredFields,
} from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  CheckboxComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  TextareaComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { printHtml } from '../print-html';

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  mobile?: string | null;
}

/**
 * Restaurant › Orders (TK-146): the waiter's screen, built for a phone first.
 * The list shows open orders; an order shows its table, waiter and customer,
 * the menu for its order type, and its dishes. Dishes picked are held on the
 * screen until saved, then priced by the menu on the server. What the branch's
 * restaurant settings require is marked, and the server refuses the same.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-order',
  standalone: true,
  imports: [
    FormsModule,
    IfCanDirective,
    CheckboxComponent,
    LookupDialogComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
    TextareaComponent,
  ],
  templateUrl: './restaurant-order.page.html',
  styleUrl: './restaurant-order.page.scss',
})
export class RestaurantOrderPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);
  private readonly settingsService = inject(RestaurantSettingsService);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly settings = signal<RestaurantSettingsView>(defaultRestaurantSettings());
  protected readonly orders = signal<RestaurantOrder[]>([]);
  protected readonly typeFilter = signal<OrderType | ''>('');
  protected readonly plan = signal<FloorPlan>({ floors: [] });
  protected readonly staff = signal<StaffMember[]>([]);
  protected readonly menu = signal<OrderingMenu | null>(null);
  protected readonly categoryId = signal<number | null>(null);
  protected readonly search = signal('');
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  /** The order on screen: null for the list, 'new' while a new one is being built. */
  protected readonly editing = signal<RestaurantOrder | 'new' | null>(null);
  protected readonly orderType = signal<OrderType>('DineIn');
  protected readonly tableId = signal<number | null>(null);
  protected readonly waiterId = signal<string | null>(null);
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected guests: number | null = null;
  protected notes = '';
  protected readonly pending = signal<PendingLine[]>([]);
  private nextKey = 1;

  // The dish being chosen.
  protected readonly choosing = signal<OrderingItem | null>(null);
  protected chosenVariant: OrderingVariant | null = null;
  protected readonly chosenModifiers = signal<number[]>([]);
  protected chosenQuantity: number | null = 1;
  protected chosenNote = '';

  // Kitchen tickets (TK-147).
  protected readonly kots = signal<Kot[]>([]);
  protected readonly cancellingLine = signal<KotLine | null>(null);
  protected lineCancelReason = '';
  protected readonly canCancel = canCancel;
  protected readonly cancelNeedsApproval = cancelNeedsApproval;

  // Moving and cancelling.
  protected readonly sheet = signal<'move' | 'cancel' | null>(null);
  protected moveTo: number | null = null;
  protected cancelReason = '';

  // Customer picker.
  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  protected readonly orderTypes = ORDER_TYPES;
  protected readonly kitchenStatusLabel = kitchenStatusLabel;
  protected readonly orderPlace = orderPlace;

  protected readonly required = computed(() => requiredFields(this.settings(), this.orderType()));
  protected readonly visibleOrders = computed(() => this.orders().filter((o) => !this.typeFilter() || o.orderType === this.typeFilter()));
  protected readonly tableOptions = computed<BbSelectOption<number>[]>(() => {
    const current = this.editing();
    const ownTable = current && current !== 'new' ? current.restaurantTableId : null;
    return allTables(this.plan())
      .filter((t) => t.isActive && t.mergedIntoTableId == null && (t.restaurantTableId === ownTable || (t.openOrderId == null && (t.status === 'Free' || t.status === 'Occupied'))))
      .map((t) => ({ value: t.restaurantTableId, label: `${t.code} · ${t.seats} seats${t.mergedCodes.length ? ` + ${t.mergedCodes.join(', ')}` : ''}` }));
  });
  protected readonly moveOptions = computed<BbSelectOption<number>[]>(() =>
    allTables(this.plan())
      .filter((t) => t.isActive && t.status === 'Free' && t.mergedIntoTableId == null && t.mergedCodes.length === 0 && t.openOrderId == null)
      .map((t) => ({ value: t.restaurantTableId, label: `${t.code} · ${t.seats} seats` })),
  );
  protected readonly staffOptions = computed<BbSelectOption<string>[]>(() => this.staff().map((s) => ({ value: s.id, label: s.name })));
  protected readonly categories = computed<OrderingCategory[]>(() => this.menu()?.categories ?? []);
  protected readonly dishes = computed<OrderingItem[]>(() => {
    const term = this.search().trim().toLowerCase();
    const all = this.categories().flatMap((c) => c.items.map((i) => ({ ...i, categoryId: c.menuCategoryId })));
    return all.filter((i) => (term ? i.name.toLowerCase().includes(term) : !this.categoryId() || i.categoryId === this.categoryId()));
  });
  protected readonly pendingTotal = computed(() => pendingTotal(this.pending()));
  protected readonly problem = computed(() =>
    orderProblem(this.settings(), { orderType: this.orderType(), restaurantTableId: this.tableId(), waiterUserId: this.waiterId(), contactId: this.contactId() }),
  );

  ngOnInit(): void {
    void this.start();
  }

  private async start(): Promise<void> {
    try {
      const [settings, staff] = await Promise.all([this.settingsService.load(), firstValueFrom(this.http.get<StaffMember[]>(`${RESTAURANT_API}/staff`))]);
      this.settings.set(settings);
      this.staff.set(staff);
      if (settings.tablesEnabled) {
        this.plan.set(await firstValueFrom(this.http.get<FloorPlan>(`${RESTAURANT_API}/floor-plan`)));
      }
    } catch (error) {
      this.fail(error, 'The order screen could not be loaded.');
    }

    await this.loadOrders();
    const params = this.route.snapshot.queryParamMap;
    const orderId = Number(params.get('order'));
    const tableId = Number(params.get('table'));
    if (orderId) {
      await this.openOrder(orderId);
    } else if (tableId) {
      this.newOrder(tableId);
    }
  }

  protected async loadOrders(): Promise<void> {
    try {
      this.orders.set(await firstValueFrom(this.http.get<RestaurantOrder[]>(`${RESTAURANT_ORDERS_API}?status=Open`)));
    } catch (error) {
      this.fail(error, 'The open orders could not be loaded.');
    }
  }

  private async loadMenu(orderType: OrderType): Promise<void> {
    try {
      const menu = await firstValueFrom(this.http.get<OrderingMenu>(`${RESTAURANT_MENU_API}/ordering?orderType=${orderType}`));
      this.menu.set(menu);
      if (!menu.categories.some((c) => c.menuCategoryId === this.categoryId())) {
        this.categoryId.set(menu.categories[0]?.menuCategoryId ?? null);
      }
    } catch (error) {
      this.fail(error, 'The menu could not be loaded.');
    }
  }

  private async refreshPlan(): Promise<void> {
    if (this.settings().tablesEnabled) {
      this.plan.set(await firstValueFrom(this.http.get<FloorPlan>(`${RESTAURANT_API}/floor-plan`)));
    }
  }

  // ---- Choosing an order ----

  protected newOrder(tableId: number | null = null): void {
    const me = this.auth.userId();
    this.editing.set('new');
    this.orderType.set(tableId || this.settings().tablesEnabled ? 'DineIn' : 'Takeaway');
    this.tableId.set(tableId);
    this.waiterId.set(me && this.staff().some((s) => s.id === me) ? me : null);
    this.contactId.set(null);
    this.contactLabel.set('');
    this.guests = null;
    this.notes = '';
    this.pending.set([]);
    this.messages.set([]);
    void this.loadMenu(this.orderType());
  }

  protected async openOrder(orderId: number): Promise<void> {
    try {
      const order = await firstValueFrom(this.http.get<RestaurantOrder>(`${RESTAURANT_ORDERS_API}/${orderId}`));
      this.show(order);
      await this.loadMenu(order.orderType);
    } catch (error) {
      this.fail(error, 'The order could not be opened.');
    }
  }

  private show(order: RestaurantOrder): void {
    if (this.current()?.restaurantOrderId !== order.restaurantOrderId) {
      void this.loadKots(order.restaurantOrderId);
    }
    this.editing.set(order);
    this.orderType.set(order.orderType);
    this.tableId.set(order.restaurantTableId);
    this.waiterId.set(order.waiterUserId);
    this.contactId.set(order.contactId);
    this.contactLabel.set(order.contactName ?? (order.contactId ? `Customer ${order.contactId}` : ''));
    this.guests = order.guests;
    this.notes = order.notes ?? '';
    this.pending.set([]);
  }

  /** To the billing screen with this order. */
  protected toBilling(): void {
    const order = this.current();
    if (order) void this.router.navigate(['/restaurant/billing'], { queryParams: { order: order.restaurantOrderId } });
  }

  protected back(): void {
    this.editing.set(null);
    this.pending.set([]);
    void this.router.navigate([], { queryParams: {} });
    void this.loadOrders();
  }

  protected current(): RestaurantOrder | null {
    const e = this.editing();
    return e && e !== 'new' ? e : null;
  }

  protected isOpen(): boolean {
    const order = this.current();
    return !order || order.status === 'Open';
  }

  // ---- The order's details ----

  protected async setOrderType(orderType: OrderType): Promise<void> {
    if (orderType === this.orderType()) return;
    const order = this.current();
    if (order) {
      await this.run(async () => {
        await firstValueFrom(this.http.put(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/order-type`, { orderType }));
        await this.reload(order.restaurantOrderId);
      }, 'Order type changed and the dishes priced again.');
    } else {
      this.orderType.set(orderType);
      this.pending.set([]);
    }
    await this.loadMenu(this.orderType());
  }

  protected async saveDetails(): Promise<void> {
    const order = this.current();
    if (!order) return;
    await this.run(async () => {
      await firstValueFrom(
        this.http.put(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}`, {
          waiterUserId: this.waiterId(),
          contactId: this.contactId(),
          guests: this.guests,
          notes: this.notes.trim() || null,
        }),
      );
      await this.reload(order.restaurantOrderId);
    }, 'Order updated.');
  }

  // ---- Dishes ----

  protected tapDish(item: OrderingItem): void {
    if (!item.isAvailable) {
      this.messages.set([{ tone: 'warning', text: `${item.name} is sold out.` }]);
      return;
    }

    if (addsInOneTap(item)) {
      this.addPending(item, null, [], 1, '');
      return;
    }

    this.choosing.set(item);
    this.chosenVariant = item.variants[0] ?? null;
    this.chosenModifiers.set([]);
    this.chosenQuantity = 1;
    this.chosenNote = '';
  }

  protected toggleModifier(id: number, on: boolean): void {
    this.chosenModifiers.update((ids) => (on ? [...ids, id] : ids.filter((x) => x !== id)));
  }

  protected chosenPrice(): number {
    const item = this.choosing();
    return item ? pendingPrice(item, this.chosenVariant, this.chosenModifiers()) : 0;
  }

  protected addChosen(): void {
    const item = this.choosing();
    if (!item) return;
    const problem = item.variants.length && !this.chosenVariant ? `Choose a portion of ${item.name}.` : choiceProblem(item.modifierGroups, this.chosenModifiers());
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }
    this.addPending(item, this.chosenVariant, this.chosenModifiers(), this.chosenQuantity && this.chosenQuantity > 0 ? this.chosenQuantity : 1, this.chosenNote);
    this.choosing.set(null);
  }

  private addPending(item: OrderingItem, variant: OrderingVariant | null, modifierIds: number[], quantity: number, note: string): void {
    const modifiers = item.modifierGroups.flatMap((g) => g.modifiers).filter((m) => modifierIds.includes(m.modifierId));
    const same = this.pending().find(
      (p) => p.itemId === item.itemId && p.itemVariantId === (variant?.itemVariantId ?? null) && p.modifierIds.join() === modifierIds.join() && !p.note && !note.trim(),
    );
    if (same) {
      this.changePending(same.key, same.quantity + quantity);
    } else {
      this.pending.update((lines) => [
        ...lines,
        {
          key: this.nextKey++,
          itemId: item.itemId,
          name: item.name,
          itemVariantId: variant?.itemVariantId ?? null,
          variantName: variant?.name ?? null,
          modifierIds: [...modifierIds],
          modifierNames: modifiers.map((m) => m.name),
          quantity,
          unitPrice: pendingPrice(item, variant, modifierIds),
          note,
        },
      ]);
    }
    this.messages.set([]);
  }

  protected changePending(key: number, quantity: number): void {
    this.pending.update((lines) => (quantity <= 0 ? lines.filter((l) => l.key !== key) : lines.map((l) => (l.key === key ? { ...l, quantity } : l))));
  }

  protected async saveOrder(): Promise<void> {
    const order = this.current();
    if (order) {
      if (!this.pending().length) return;
      await this.run(async () => {
        await firstValueFrom(this.http.post(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/lines`, { lines: linesRequest(this.pending()) }));
        await this.reload(order.restaurantOrderId);
      }, 'Added to the order.');
      return;
    }

    if (this.problem()) {
      this.messages.set([{ tone: 'error', text: this.problem()! }]);
      return;
    }

    await this.run(async () => {
      const created = await firstValueFrom(
        this.http.post<{ restaurantOrderId: number }>(RESTAURANT_ORDERS_API, {
          orderType: this.orderType(),
          restaurantTableId: this.settings().tablesEnabled ? this.tableId() : null,
          waiterUserId: this.waiterId(),
          contactId: this.contactId(),
          guests: this.guests,
          notes: this.notes.trim() || null,
          lines: linesRequest(this.pending()),
        }),
      );
      await this.reload(created.restaurantOrderId);
      void this.router.navigate([], { queryParams: { order: created.restaurantOrderId } });
    }, 'Order saved.');
  }

  protected async changeSaved(line: RestaurantOrderLine, quantity: number): Promise<void> {
    const order = this.current();
    if (!order) return;
    await this.run(async () => {
      if (quantity <= 0) {
        await firstValueFrom(this.http.delete(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/lines/${line.restaurantOrderLineId}`));
      } else {
        await firstValueFrom(this.http.put(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/lines/${line.restaurantOrderLineId}`, { quantity, note: line.note }));
      }
      await this.reload(order.restaurantOrderId);
    }, quantity <= 0 ? `${line.itemName} taken off.` : 'Quantity changed.');
  }

  // ---- Moving and cancelling ----

  protected async move(): Promise<void> {
    const order = this.current();
    if (!order || !this.moveTo) return;
    const to = this.moveTo;
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/move`, { restaurantTableId: to }));
      this.sheet.set(null);
      await this.reload(order.restaurantOrderId);
    }, 'Order moved.');
  }

  protected async cancel(): Promise<void> {
    const order = this.current();
    if (!order) return;
    if (!this.cancelReason.trim()) {
      this.messages.set([{ tone: 'error', text: 'Say why the order is cancelled.' }]);
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/cancel`, { reason: this.cancelReason.trim() }));
      this.sheet.set(null);
      this.back();
    }, `Order ${order.orderNo} cancelled.`);
  }

  // ---- Customer ----

  protected openPicker(): void {
    this.pickerOpen.set(true);
    void this.searchContacts('');
  }

  protected async searchContacts(term: string): Promise<void> {
    this.pickerLoading.set(true);
    try {
      const query = new URLSearchParams({ role: 'customer', includeInactive: 'false' });
      if (term.trim()) query.set('search', term.trim());
      const rows = await firstValueFrom(this.http.get<ContactOption[]>(`/api/contacts?${query}`));
      this.pickerRows.set(rows.map((c) => ({ id: c.contactId, code: c.contactCode, name: c.displayName, meta: c.mobile ?? null })));
    } catch {
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected pickContact(row: LookupRow): void {
    this.contactId.set(row.id);
    this.contactLabel.set(row.name);
    this.pickerOpen.set(false);
  }

  protected clearContact(): void {
    this.contactId.set(null);
    this.contactLabel.set('');
  }

  // ---- Shared ----

  protected modifierNames(line: RestaurantOrderLine): string {
    return line.modifiers.map((m) => m.name).join(', ');
  }

  protected money(value: number): string {
    return this.formats.formatMoney(value);
  }

  private async reload(orderId: number): Promise<void> {
    this.show(await firstValueFrom(this.http.get<RestaurantOrder>(`${RESTAURANT_ORDERS_API}/${orderId}`)));
    await Promise.all([this.refreshPlan(), this.loadKots(orderId)]);
  }

  // ---- Kitchen tickets (TK-147) ----

  private async loadKots(orderId: number): Promise<void> {
    if (!this.settings().kitchenEnabled) {
      this.kots.set([]);
      return;
    }
    try {
      this.kots.set(await firstValueFrom(this.http.get<Kot[]>(`${RESTAURANT_ORDERS_API}/${orderId}/kots`)));
    } catch {
      this.kots.set([]);
    }
  }

  /** Sends the new dishes, one ticket per station, and prints each ticket. */
  protected async sendToKitchen(): Promise<void> {
    const order = this.current();
    if (!order) return;
    await this.run(async () => {
      const sent = await firstValueFrom(this.http.post<{ kotIds: number[] }>(`${RESTAURANT_ORDERS_API}/${order.restaurantOrderId}/send`, {}));
      for (const kotId of sent.kotIds) {
        await this.printKot(kotId);
      }
      await this.reload(order.restaurantOrderId);
    }, 'Sent to the kitchen.');
  }

  protected async reprintKot(kot: Kot): Promise<void> {
    await this.run(async () => {
      await this.printKot(kot.kotId);
      await this.loadKots(kot.restaurantOrderId);
    }, `${kot.kotNo} reprinted.`);
  }

  private async printKot(kotId: number): Promise<void> {
    const printed = await firstValueFrom(this.http.post<PrintedDocument>(`${RESTAURANT_API}/kots/${kotId}/print`, {}));
    printHtml(printed.html);
  }

  protected startLineCancel(line: KotLine): void {
    this.lineCancelReason = '';
    this.cancellingLine.set(line);
  }

  protected async cancelLine(): Promise<void> {
    const line = this.cancellingLine();
    const order = this.current();
    if (!line || !order) return;
    if (!this.lineCancelReason.trim()) {
      this.messages.set([{ tone: 'error', text: 'Say why the dish is cancelled.' }]);
      return;
    }
    await this.run(async () => {
      await firstValueFrom(this.http.post(`${RESTAURANT_API}/kot-lines/${line.kotLineId}/cancel`, { reason: this.lineCancelReason.trim() }));
      this.cancellingLine.set(null);
      await this.reload(order.restaurantOrderId);
    }, `${line.itemName} cancelled.`);
  }

  private async run(work: () => Promise<void>, done: string): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: done }]);
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
