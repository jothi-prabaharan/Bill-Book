import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { IfCanDirective } from '@bill-book/auth';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BillPreview,
  PrintedDocument,
  RESTAURANT_BILLS_API,
  RESTAURANT_ORDERS_API,
  RestaurantBill,
  RestaurantOrder,
  RestaurantOrderLine,
  RestaurantSettingsService,
  RestaurantSettingsView,
  TENDER_MODES,
  TenderAccount,
  TenderDraft,
  TenderMode,
  billRequest,
  changeDue,
  defaultAccount,
  defaultRestaurantSettings,
  orderPlace,
  tenderProblem,
  tipProblem,
} from '@bill-book/restaurant-core';
import {
  BbSelectOption,
  CheckboxComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  MoneyInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { printHtml } from '../print-html';

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
}

/**
 * Restaurant › Billing (TK-148). An order is billed on a sales invoice: every
 * dish, or some of them to split the bill, or several orders together to
 * merge tables' bills. The service charge, the tip and the payments are set
 * here; the preview is worked out by the server with the invoice's own tax
 * calculator, and billing posts the invoice and prints it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-restaurant-billing',
  standalone: true,
  imports: [FormsModule, IfCanDirective, CheckboxComponent, LookupDialogComponent, MessageBoxComponent, MoneyInputComponent, SelectComponent, TextInputComponent],
  templateUrl: './restaurant-billing.page.html',
  styleUrl: './restaurant-billing.page.scss',
})
export class RestaurantBillingPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly settingsService = inject(RestaurantSettingsService);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly settings = signal<RestaurantSettingsView>(defaultRestaurantSettings());
  protected readonly openOrders = signal<RestaurantOrder[]>([]);
  protected readonly orders = signal<RestaurantOrder[]>([]);
  protected readonly accounts = signal<TenderAccount[]>([]);
  protected readonly preview = signal<BillPreview | null>(null);
  protected readonly bill = signal<RestaurantBill | null>(null);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected readonly selected = signal<Set<number>>(new Set());
  protected applyServiceCharge = true;
  protected tipAmount: number | null = null;
  protected tipBankAccountId: number | null = null;
  protected readonly tenders = signal<TenderDraft[]>([]);
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected addOrderId: number | null = null;

  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  protected readonly modeOptions: BbSelectOption<string>[] = TENDER_MODES.map((m) => ({ value: m.value, label: m.label }));
  protected readonly orderPlace = orderPlace;

  protected readonly billableLines = computed(() =>
    this.orders().flatMap((o) => o.lines.filter((l) => l.kitchenStatus !== 'Cancelled' && !this.isBilled(l)).map((l) => ({ order: o, line: l }))),
  );
  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.accounts().map((a) => ({ value: a.bankAccountId, label: a.maskedNumber ? `${a.accountName} ····${a.maskedNumber}` : a.accountName })),
  );
  protected readonly otherOrders = computed<BbSelectOption<number>[]>(() =>
    this.openOrders()
      .filter((o) => !this.orders().some((x) => x.restaurantOrderId === o.restaurantOrderId))
      .map((o) => ({ value: o.restaurantOrderId, label: `${orderPlace(o)} · ${o.orderNo}` })),
  );
  protected readonly change = computed(() => {
    const p = this.preview();
    return p ? changeDue(p.total, this.tenders()) : null;
  });

  ngOnInit(): void {
    void this.start();
  }

  private async start(): Promise<void> {
    try {
      const [settings, accounts, open] = await Promise.all([
        this.settingsService.load(),
        firstValueFrom(this.http.get<TenderAccount[]>(`${RESTAURANT_BILLS_API}/tender-accounts`)),
        firstValueFrom(this.http.get<RestaurantOrder[]>(`${RESTAURANT_ORDERS_API}?status=Open`)),
      ]);
      this.settings.set(settings);
      this.accounts.set(accounts);
      this.openOrders.set(open);
      this.tipBankAccountId = defaultAccount('Cash', accounts);
    } catch (error) {
      this.fail(error, 'The billing screen could not be loaded.');
    }

    const orderId = Number(this.route.snapshot.queryParamMap.get('order'));
    if (orderId) {
      await this.addOrder(orderId);
    }
  }

  protected isBilled(line: RestaurantOrderLine): boolean {
    return line.restaurantBillId !== null;
  }

  protected async addOrder(orderId: number | null): Promise<void> {
    if (!orderId) return;
    try {
      const order = await firstValueFrom(this.http.get<RestaurantOrder>(`${RESTAURANT_ORDERS_API}/${orderId}`));
      this.orders.update((list) => [...list.filter((o) => o.restaurantOrderId !== order.restaurantOrderId), order]);
      const next = new Set(this.selected());
      order.lines.filter((l) => l.kitchenStatus !== 'Cancelled' && !this.isBilled(l)).forEach((l) => next.add(l.restaurantOrderLineId));
      this.selected.set(next);
      if (!this.contactId() && order.contactId) {
        this.contactId.set(order.contactId);
        this.contactLabel.set(order.contactName ?? `Customer ${order.contactId}`);
      }
      this.addOrderId = null;
      await this.refresh();
    } catch (error) {
      this.fail(error, 'The order could not be added.');
    }
  }

  protected async removeOrder(order: RestaurantOrder): Promise<void> {
    this.orders.update((list) => list.filter((o) => o.restaurantOrderId !== order.restaurantOrderId));
    const next = new Set(this.selected());
    order.lines.forEach((l) => next.delete(l.restaurantOrderLineId));
    this.selected.set(next);
    await this.refresh();
  }

  protected async toggleLine(id: number, on: boolean): Promise<void> {
    const next = new Set(this.selected());
    if (on) next.add(id);
    else next.delete(id);
    this.selected.set(next);
    await this.refresh();
  }

  protected addTender(mode: TenderMode = 'Cash'): void {
    const p = this.preview();
    const remaining = p ? Math.max(0, Math.round((p.total - this.tenders().reduce((s, t) => s + (t.amount ?? 0), 0)) * 100) / 100) : null;
    this.tenders.update((list) => [...list, { mode, amount: remaining || null, bankAccountId: defaultAccount(mode, this.accounts()), reference: '' }]);
  }

  protected setTender(index: number, patch: Partial<TenderDraft>): void {
    this.tenders.update((list) =>
      list.map((t, i) => {
        if (i !== index) return t;
        const next = { ...t, ...patch };
        if (patch.mode && patch.mode !== t.mode) next.bankAccountId = defaultAccount(patch.mode, this.accounts());
        return next;
      }),
    );
  }

  protected removeTender(index: number): void {
    this.tenders.update((list) => list.filter((_, i) => i !== index));
  }

  protected async refresh(): Promise<void> {
    if (!this.orders().length || !this.selected().size) {
      this.preview.set(null);
      return;
    }
    try {
      this.preview.set(await firstValueFrom(this.http.post<BillPreview>(`${RESTAURANT_BILLS_API}/preview`, this.body())));
    } catch (error) {
      this.preview.set(null);
      this.fail(error, 'The bill could not be worked out.');
    }
  }

  private body() {
    return billRequest(
      {
        orderIds: this.orders().map((o) => o.restaurantOrderId),
        lineIds: [...this.selected()],
        contactId: this.contactId(),
        applyServiceCharge: this.applyServiceCharge,
        tipAmount: this.tipAmount,
        tipBankAccountId: this.tipBankAccountId,
        tenders: this.tenders(),
      },
      this.billableLines().map((x) => x.line.restaurantOrderLineId),
    );
  }

  protected async billIt(): Promise<void> {
    const p = this.preview();
    if (!p) return;
    const problem = p.problem ?? tipProblem(this.settings().tipsEnabled, this.tipAmount, this.tipBankAccountId) ?? tenderProblem(p.total, this.tenders());
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    this.busy.set(true);
    try {
      const bill = await firstValueFrom(this.http.post<RestaurantBill>(RESTAURANT_BILLS_API, this.body()));
      this.bill.set(bill);
      this.messages.set([{ tone: 'success', text: `Billed on ${bill.invoiceNo}.` }]);
      await this.print(bill);
    } catch (error) {
      this.fail(error, 'The bill could not be made.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async print(bill: RestaurantBill): Promise<void> {
    try {
      const printed = await firstValueFrom(this.http.get<PrintedDocument>(`${RESTAURANT_BILLS_API}/${bill.restaurantBillId}/print`));
      printHtml(printed.html);
    } catch (error) {
      this.fail(error, 'The bill was made but could not be printed. Print it again from the invoice.');
    }
  }

  protected done(): void {
    void this.router.navigate(['/restaurant/orders']);
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
      this.pickerRows.set(rows.map((c) => ({ id: c.contactId, code: c.contactCode, name: c.displayName })));
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
    void this.refresh();
  }

  protected clearContact(): void {
    this.contactId.set(null);
    this.contactLabel.set('');
    void this.refresh();
  }

  protected money(value: number): string {
    return this.formats.formatMoney(value);
  }

  protected lineLabel(line: RestaurantOrderLine): string {
    const name = line.variantName ? `${line.itemName} (${line.variantName})` : line.itemName;
    return line.modifiers.length ? `${name}: ${line.modifiers.map((m) => m.name).join(', ')}` : name;
  }

  private fail(error: unknown, fallback: string): void {
    const message = error instanceof HttpErrorResponse ? (error.error?.message as string | undefined) : undefined;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
