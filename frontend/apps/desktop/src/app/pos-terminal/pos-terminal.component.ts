import { ChangeDetectionStrategy } from '@angular/core';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import { InvoiceService, SaveInvoiceRequest, toApiLine } from '@bill-book/sales-core';
import {
  DocumentLine,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  TaxGroupOption,
  UiMessage,
} from '@bill-book/ui-components';
import { EscPosService } from './esc-pos.service';
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

/**
 * The till: a cart of real items for a real customer.
 *
 * **A POS sale is an ordinary invoice with a till on it** — an `sal.Invoices`
 * row with `TransactionTypeCode = 'POS'` — so the cart holds invoice lines and
 * every figure on it comes from `line-math.ts`, the same arithmetic the invoice
 * form and the C# calculator share. The GST shown here is a preview; the server
 * recomputes it on save.
 *
 * The customer defaults to the branch's walk-in contact, found by its code
 * rather than by an id that differs in every database. Choosing another
 * customer can move the sale across a state line, which rebuilds every line's
 * tax rows rather than just recalculating them.
 *
 * Posting the sale — tender, till, change, the POS transaction type — is TK-39.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-pos-terminal',
  standalone: true,
  imports: [LookupDialogComponent, MessageBoxComponent],
  templateUrl: './pos-terminal.component.html',
  styleUrl: './pos-terminal.component.scss',
})
export class PosTerminalComponent implements OnInit {
  private readonly invoiceService = inject(InvoiceService);
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

  protected readonly canCheckout = computed(
    () => !this.busy() && this.customer() !== null && this.lines().length > 0,
  );

  protected readonly pickerTitle = computed(() =>
    this.picker() === 'customer' ? 'Choose a customer' : 'Add an item',
  );

  protected readonly pickerPlaceholder = computed(() =>
    this.picker() === 'customer' ? 'Name, code or GSTIN' : 'Item name or code',
  );

  ngOnInit(): void {
    void this.formats.load();
    void this.load();
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
      const item = await this.lookups.cartItem(row.id);
      const group = this.taxGroups().find(
        (candidate) => candidate.taxGroupId === item.taxGroupId,
      );
      this.lines.set(addItem(this.lines(), item, group, this.context()));
    } catch (error) {
      this.messages.set([this.failure(error)]);
    }
  }

  /** Sets the customer and redoes the tax split, since the state line may move. */
  private chooseCustomer(customer: CustomerOption): void {
    this.customer.set(customer);
    this.lines.set(reprice(this.lines(), this.taxGroups(), this.context()));
    this.messages.set([]);
  }

  // ---- Checkout -------------------------------------------------------------

  /**
   * Sends the cart as an invoice and prints its receipt.
   *
   * Still the scaffold's path: tender, till and the POS transaction type are
   * TK-39, and until then the server may refuse what this sends. The refusal
   * is shown in the server's own words rather than swallowed.
   */
  async checkout(): Promise<void> {
    const customer = this.customer();
    if (!this.canCheckout() || customer === null) {
      return;
    }

    const lines = this.lines();
    const request: SaveInvoiceRequest = {
      documentDate: new Date().toISOString().split('T')[0],
      contactId: customer.contactId,
      contactGstin: customer.gstin ?? undefined,
      exchangeRate: 1,
      lines: lines.map(toApiLine),
    };

    this.busy.set(true);
    try {
      await this.invoiceService.create(request);

      const receiptBytes = this.escPosService.generateReceipt(
        'BILL-BOOK STORE',
        lines.map((line) => ({
          name: line.description ?? line.itemLabel ?? 'Item',
          amount: line.lineTotal / 100,
        })),
        this.totals().totalAmount / 100,
      );

      this.printReceipt(receiptBytes);
      this.lines.set([]);
      this.messages.set([{ tone: 'success', text: 'Sale saved.' }]);
    } catch (error) {
      this.messages.set([this.failure(error)]);
    } finally {
      this.busy.set(false);
    }
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
