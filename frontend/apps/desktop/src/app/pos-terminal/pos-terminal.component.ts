import { ChangeDetectionStrategy, Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import { toApiLine } from '@bill-book/sales-core';
import {
  DocumentLine,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  TaxGroupOption,
  UiMessage,
} from '@bill-book/ui-components';
import { EscPosService } from './esc-pos.service';
import { BarcodeBurst, POS_KEYS, PosCommand, commandFor } from './pos-keys';
import { PosSaleService, TenderAccount } from './pos-sale.service';
import { TenderLine, TenderMode, roundToRupee, tenderSummary } from './pos-tender';
import {
  QTY_SCALE,
  addItem,
  cartContext,
  cartTotals,
  isInterStateSale,
  removeLine,
  reprice,
  setQuantity,
  setUnitPrice,
} from './pos-cart';
import {
  CustomerOption,
  PosLookupService,
  TillBranch,
  WALK_IN_CONTACT_CODE,
} from './pos-lookup.service';

type Picker = 'none' | 'customer' | 'item';

/** A cart put aside with F8, to be recalled with F7. Kept in memory only. */
interface HeldSale {
  id: number;
  lines: DocumentLine[];
  customer: CustomerOption | null;
  heldAt: Date;
}

const TILL_KEY = 'bb.pos.tillId';

/**
 * The till: a cart of real items for a real customer, paid and posted in one
 * call (TK-40, over TK-39's `POST api/sales/pos/sales`).
 *
 * **A POS sale is an ordinary invoice with a till on it**, so the cart holds
 * invoice lines and every figure on it comes from `line-math.ts`, the same
 * arithmetic the invoice form and the C# calculator share. The GST shown is a
 * preview; the server recomputes it, and rounds the total to the rupee.
 *
 * **Keyboard first.** F2 adds an item, F3 changes the customer, F4 edits the
 * selected line's quantity, F6 voids it, F8 holds the cart and F7 recalls a held
 * one, F9 opens the tender; the arrow keys move the selection and Escape closes
 * whatever is open. A barcode scanner's burst of keystrokes adds the item it
 * names (`BarcodeBurst`).
 *
 * **Offline, it refuses to sell** (owner's decision, 24 September 2026). There
 * is no local queue: a sale is either posted, with stock taken, or not made.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-pos-terminal',
  standalone: true,
  imports: [LookupDialogComponent, MessageBoxComponent],
  templateUrl: './pos-terminal.component.html',
  styleUrl: './pos-terminal.component.scss',
})
export class PosTerminalComponent implements OnInit, OnDestroy {
  private readonly posSales = inject(PosSaleService);
  private readonly escPosService = inject(EscPosService);
  private readonly lookups = inject(PosLookupService);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly lines = signal<DocumentLine[]>([]);
  protected readonly customer = signal<CustomerOption | null>(null);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  private readonly branch = signal<TillBranch>({ gstin: null, discountBeforeTax: true });
  private readonly taxGroups = signal<TaxGroupOption[]>([]);

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);
  private searchToken = 0;
  /** The last customer search, so a chosen row can be mapped back to its GSTIN. */
  private customerRows: CustomerOption[] = [];

  protected readonly context = computed(() =>
    cartContext(
      isInterStateSale(this.branch().gstin, this.customer()?.gstin),
      this.branch().discountBeforeTax,
    ),
  );

  protected readonly totals = computed(() => cartTotals(this.lines()));

  protected readonly isInterState = computed(() => this.context().isInterState);

  protected readonly keys = POS_KEYS;

  /** The line F4 and F6 act on. */
  protected readonly selected = signal(0);

  protected readonly online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);

  protected readonly tillId = signal(PosTerminalComponent.readTillId());

  protected readonly held = signal<HeldSale[]>([]);
  private heldSeq = 0;

  // ---- Tender ---------------------------------------------------------------

  protected readonly tenderOpen = signal(false);
  protected readonly tenders = signal<TenderLine[]>([]);
  protected readonly tenderAccounts = signal<TenderAccount[]>([]);
  protected readonly tenderMode = signal<TenderMode>('Cash');
  protected readonly tenderAccountId = signal<number | null>(null);
  /** Rupees, as typed. */
  protected readonly tenderAmount = signal('');
  protected readonly tenderReference = signal('');

  /** What the customer pays: the cart total rounded to the rupee, in paise. */
  protected readonly payable = computed(() => roundToRupee(this.totals().totalAmount));

  protected readonly roundOff = computed(() => this.payable() - this.totals().totalAmount);

  protected readonly tenderState = computed(() => tenderSummary(this.payable(), this.tenders()));

  protected readonly accountsForMode = computed(() =>
    this.tenderAccounts().filter((a) => (this.tenderMode() === 'Cash') === (a.accountType === 'Cash')),
  );

  protected readonly canCheckout = computed(
    () => !this.busy() && this.online() && this.customer() !== null && this.lines().length > 0,
  );

  protected readonly canComplete = computed(
    () => this.canCheckout() && this.tenderState().problem === null,
  );

  private readonly burst = new BarcodeBurst();
  private readonly onOnline = () => this.online.set(true);
  private readonly onOffline = () => this.online.set(false);

  protected readonly pickerTitle = computed(() =>
    this.picker() === 'customer' ? 'Choose a customer' : 'Add an item',
  );

  protected readonly pickerPlaceholder = computed(() =>
    this.picker() === 'customer' ? 'Name, code or GSTIN' : 'Item name or code',
  );

  ngOnInit(): void {
    void this.formats.load();
    void this.load();

    if (typeof window !== 'undefined') {
      window.addEventListener('online', this.onOnline);
      window.addEventListener('offline', this.onOffline);
    }
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined') {
      window.removeEventListener('online', this.onOnline);
      window.removeEventListener('offline', this.onOffline);
    }
  }

  private static readTillId(): number {
    try {
      const stored = Number(localStorage.getItem(TILL_KEY));
      return Number.isInteger(stored) && stored > 0 ? stored : 1;
    } catch {
      return 1;
    }
  }

  protected setTillId(value: string): void {
    const id = Number(value);
    if (!Number.isInteger(id) || id <= 0) {
      return;
    }
    this.tillId.set(id);
    try {
      localStorage.setItem(TILL_KEY, String(id));
    } catch {
      // A per-device convenience; the till still works on its default.
    }
  }

  // ---- Keyboard -------------------------------------------------------------

  @HostListener('document:keydown', ['$event'])
  protected onKey(event: KeyboardEvent): void {
    const target = event.target as HTMLElement | null;
    const typing = !!target && ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);

    // A scan arrives as fast keystrokes wherever the focus is not a field.
    if (!typing && this.picker() === 'none' && !this.tenderOpen()) {
      const code = this.burst.push(event.key, event.timeStamp || Date.now());
      if (code !== null) {
        event.preventDefault();
        void this.scan(code);
        return;
      }
    }

    const command = commandFor(event);
    if (command === null || (typing && !event.key.startsWith('F') && command !== 'cancel')) {
      return;
    }

    event.preventDefault();
    this.run(command);
  }

  protected run(command: PosCommand): void {
    const count = this.lines().length;

    switch (command) {
      case 'addItem':
        this.openItemPicker();
        break;
      case 'customer':
        this.openCustomerPicker();
        break;
      case 'quantity':
        this.focusQuantity(this.selected());
        break;
      case 'voidLine':
        if (count > 0) {
          this.remove(Math.min(this.selected(), count - 1));
        }
        break;
      case 'hold':
        this.hold();
        break;
      case 'recall':
        this.recall();
        break;
      case 'tender':
        void this.openTender();
        break;
      case 'selectUp':
        this.selected.set(Math.max(0, this.selected() - 1));
        break;
      case 'selectDown':
        this.selected.set(Math.min(Math.max(0, count - 1), this.selected() + 1));
        break;
      case 'cancel':
        if (this.tenderOpen()) {
          this.closeTender();
        } else if (this.picker() !== 'none') {
          this.closePicker();
        }
        break;
    }
  }

  private focusQuantity(index: number): void {
    setTimeout(() =>
      document.querySelectorAll<HTMLInputElement>('.cart-qty input')[index]?.select(),
    );
  }

  /**
   * A scanned barcode: the item whose code is exactly the scan, or the only
   * match for it; otherwise the item picker opens on the scan, so a code that
   * matches several items never adds the wrong one.
   */
  protected async scan(code: string): Promise<void> {
    try {
      const matches = await this.lookups.items(code);
      const exact = matches.find((m) => m.itemCode.toUpperCase() === code.toUpperCase());
      const item = exact ?? (matches.length === 1 ? matches[0] : null);

      if (item === null) {
        this.picker.set('item');
        void this.runSearch(code);
        return;
      }

      await this.addToCart(item.itemId);
    } catch (error) {
      this.messages.set([this.failure(error)]);
    }
  }

  // ---- Hold and recall ------------------------------------------------------

  protected hold(): void {
    if (this.lines().length === 0) {
      return;
    }
    this.held.set([
      ...this.held(),
      { id: ++this.heldSeq, lines: this.lines(), customer: this.customer(), heldAt: new Date() },
    ]);
    this.lines.set([]);
    this.selected.set(0);
    this.messages.set([{ tone: 'info', text: 'Sale held. Press F7 to recall it.' }]);
  }

  /** Brings back the most recent held sale, or a chosen one, when the cart is empty. */
  protected recall(id?: number): void {
    const held = this.held();
    const sale = id === undefined ? held[held.length - 1] : held.find((h) => h.id === id);
    if (!sale) {
      return;
    }
    if (this.lines().length > 0) {
      this.messages.set([{ tone: 'warning', text: 'Finish or hold the current sale before recalling another.' }]);
      return;
    }
    this.held.set(held.filter((h) => h.id !== sale.id));
    if (sale.customer) {
      this.customer.set(sale.customer);
    }
    this.lines.set(reprice(sale.lines, this.taxGroups(), this.context()));
    this.selected.set(0);
  }

  /**
   * The branch, its sales rates and the walk-in customer, in parallel.
   *
   * Settled rather than all-or-nothing: a branch with no walk-in customer can
   * still sell to a chosen one, and a failed rate read still lets the cashier
   * see what is wrong instead of a blank terminal.
   */
  private async load(): Promise<void> {

    const [branch, groups, walkIn] = await Promise.allSettled([
      this.lookups.branch(),
      this.lookups.salesTaxGroups(),
      this.lookups.walkInCustomer(),
    ]);

    const problems: UiMessage[] = [];

    if (branch.status === 'fulfilled') {
      this.branch.set(branch.value);
    } else {
      problems.push(this.failure(branch.reason));
    }

    if (groups.status === 'fulfilled') {
      this.taxGroups.set(groups.value);
    } else {
      problems.push(this.failure(groups.reason));
    }

    if (walkIn.status === 'fulfilled' && walkIn.value !== null) {
      this.customer.set(walkIn.value);
    } else if (walkIn.status === 'fulfilled') {
      problems.push({
        tone: 'warning',
        text: `This branch has no walk-in customer (contact code ${WALK_IN_CONTACT_CODE}). Choose a customer for each sale.`,
      });
    } else {
      problems.push(this.failure(walkIn.reason));
    }

    this.messages.set(problems);
  }

  // ---- Cart ---------------------------------------------------------------

  protected quantityOf(line: DocumentLine): number {
    return line.quantity / QTY_SCALE;
  }

  protected rupees(paise: number): string {
    return this.formats.formatMoney(paise / 100);
  }

  protected unitPriceOf(line: DocumentLine): number {
    return line.unitPrice / 100;
  }

  protected onQuantity(index: number, value: string): void {
    this.lines.set(setQuantity(this.lines(), index, Number(value), this.context()));
  }

  protected increment(index: number, by: number): void {
    const line = this.lines()[index];
    this.lines.set(
      setQuantity(this.lines(), index, this.quantityOf(line) + by, this.context()),
    );
  }

  protected onUnitPrice(index: number, value: string): void {
    this.lines.set(setUnitPrice(this.lines(), index, Number(value), this.context()));
  }

  protected remove(index: number): void {
    this.lines.set(removeLine(this.lines(), index));
  }

  protected clearCart(): void {
    this.lines.set([]);
  }

  /** Whether a taxable line found no current sales rate for its group. */
  protected missingRate(line: DocumentLine): boolean {
    return line.taxTreatment === 'Taxable' && line.taxes.length === 0;
  }

  // ---- Pickers --------------------------------------------------------------

  protected openCustomerPicker(): void {
    this.picker.set('customer');
    void this.runSearch('');
  }

  protected openItemPicker(): void {
    this.picker.set('item');
    void this.runSearch('');
  }

  protected closePicker(): void {
    this.picker.set('none');
    this.pickerRows.set([]);
  }

  protected onPickerSearch(term: string): void {
    void this.runSearch(term);
  }

  private async runSearch(term: string): Promise<void> {
    const token = ++this.searchToken;
    const picker = this.picker();
    this.pickerLoading.set(true);

    try {
      let rows: LookupRow[];

      if (picker === 'customer') {
        const customers = await this.lookups.customers(term);
        if (token !== this.searchToken) {
          return;
        }
        this.customerRows = customers;
        rows = customers.map((customer) => ({
          id: customer.contactId,
          code: customer.contactCode,
          name: customer.displayName,
          meta: customer.gstin,
        }));
      } else {
        const items = await this.lookups.items(term);
        rows = items.map((item) => ({
          id: item.itemId,
          code: item.itemCode,
          name: item.itemName,
          meta: item.inventoryUomCode,
        }));
      }

      // A slower, older search must not overwrite a newer one's rows.
      if (token === this.searchToken) {
        this.pickerRows.set(rows);
      }
    } catch (error) {
      if (token === this.searchToken) {
        this.messages.set([this.failure(error)]);
      }
    } finally {
      if (token === this.searchToken) {
        this.pickerLoading.set(false);
      }
    }
  }

  protected async onPickerChoose(row: LookupRow): Promise<void> {
    const picker = this.picker();
    this.closePicker();

    if (picker === 'customer') {
      const chosen = this.customerRows.find((customer) => customer.contactId === row.id);
      if (chosen) {
        this.chooseCustomer(chosen);
      }
      return;
    }

    try {
      await this.addToCart(row.id);
    } catch (error) {
      this.messages.set([this.failure(error)]);
    }
  }

  private async addToCart(itemId: number): Promise<void> {
    const item = await this.lookups.cartItem(itemId);
    const group = this.taxGroups().find(
      (candidate) => candidate.taxGroupId === item.taxGroupId,
    );
    this.lines.set(addItem(this.lines(), item, group, this.context()));
    this.selected.set(Math.max(0, this.lines().findIndex((line) => line.itemId === itemId)));
  }

  /** Sets the customer and redoes the tax split, since the state line may move. */
  private chooseCustomer(customer: CustomerOption): void {
    this.customer.set(customer);
    this.lines.set(reprice(this.lines(), this.taxGroups(), this.context()));
    this.messages.set([]);
  }

  // ---- Tender and checkout --------------------------------------------------

  /** F9: opens the tender, with the whole amount ready in cash. */
  async openTender(): Promise<void> {
    if (!this.canCheckout()) {
      if (!this.online()) {
        this.messages.set([this.offlineMessage()]);
      }
      return;
    }

    if (this.tenderAccounts().length === 0) {
      try {
        this.tenderAccounts.set(await this.posSales.tenderAccounts());
      } catch (error) {
        this.messages.set([this.failure(error)]);
        return;
      }
    }

    this.tenders.set([]);
    this.tenderOpen.set(true);
    this.chooseMode('Cash');
  }

  protected closeTender(): void {
    this.tenderOpen.set(false);
    this.tenders.set([]);
  }

  protected chooseMode(mode: TenderMode): void {
    this.tenderMode.set(mode);
    this.tenderAccountId.set(PosSaleService.defaultAccountFor(mode, this.tenderAccounts())?.bankAccountId ?? null);
    this.tenderAmount.set((this.tenderState().remaining / 100).toFixed(2));
    this.tenderReference.set('');
  }

  protected addTender(): void {
    const amount = Math.round(Number(this.tenderAmount()) * 100);
    const account = this.tenderAccountId();
    if (!Number.isFinite(amount) || amount <= 0 || account === null) {
      this.messages.set([{ tone: 'warning', text: 'Enter an amount and choose where the money went.' }]);
      return;
    }

    this.tenders.set([
      ...this.tenders(),
      {
        mode: this.tenderMode(),
        amount,
        bankAccountId: account,
        reference: this.tenderReference().trim() || undefined,
      },
    ]);
    this.tenderAmount.set((this.tenderState().remaining / 100).toFixed(2));
    this.tenderReference.set('');
  }

  protected removeTender(index: number): void {
    this.tenders.set(this.tenders().filter((_, at) => at !== index));
  }

  protected accountName(id: number): string {
    return this.tenderAccounts().find((a) => a.bankAccountId === id)?.accountName ?? `Account ${id}`;
  }

  /**
   * Posts the sale through TK-39 and prints its receipt. The server is the
   * authority: a 409 names an item another till sold first, a 422 means the
   * tenders do not pay its total, and either way nothing was sold.
   */
  async complete(): Promise<void> {
    const customer = this.customer();
    if (!this.canComplete() || customer === null) {
      if (!this.online()) {
        this.messages.set([this.offlineMessage()]);
      }
      return;
    }

    const lines = this.lines();
    this.busy.set(true);

    try {
      const result = await this.posSales.sell({
        tillId: this.tillId(),
        documentDate: PosTerminalComponent.today(),
        contactId: customer.contactId,
        contactGstin: customer.gstin ?? undefined,
        lines: lines.map(toApiLine),
        tenders: this.tenders().map((t) => ({
          mode: t.mode,
          amount: t.amount / 100,
          bankAccountId: t.bankAccountId,
          reference: t.reference,
        })),
      });

      const receiptBytes = this.escPosService.generateReceipt(
        'BILL-BOOK STORE',
        lines.map((line) => ({
          name: line.description ?? line.itemLabel ?? 'Item',
          amount: line.lineTotal / 100,
        })),
        result.totalAmount,
      );
      this.printReceipt(receiptBytes);

      this.lines.set([]);
      this.selected.set(0);
      this.closeTender();
      this.messages.set([
        {
          tone: 'success',
          text: result.changeAmount > 0
            ? `Sale ${result.documentNo} posted. Change: ${this.formats.formatMoney(result.changeAmount)}.`
            : `Sale ${result.documentNo} posted.`,
        },
      ]);
    } catch (error) {
      this.messages.set([navigator.onLine === false ? this.offlineMessage() : this.failure(error)]);
    } finally {
      this.busy.set(false);
    }
  }

  /** Kept for the Checkout button: it opens the tender. */
  async checkout(): Promise<void> {
    await this.openTender();
  }

  private offlineMessage(): UiMessage {
    return {
      tone: 'error',
      text: 'The till is offline. Sales are refused until the connection is back; nothing is queued.',
    };
  }

  private static today(): string {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }

  private printReceipt(bytes: Uint8Array) {
    // In a real desktop app (e.g. Electron/Tauri), this would be sent to the main process
    // which talks to the physical serial/USB thermal printer.
    console.log('Printing receipt. Bytes generated:', bytes.length);
  }

  private failure(error: unknown): UiMessage {
    const failure = readApiFailure(error);
    return { tone: 'error', text: failure.text, detail: failure.detail };
  }
}
