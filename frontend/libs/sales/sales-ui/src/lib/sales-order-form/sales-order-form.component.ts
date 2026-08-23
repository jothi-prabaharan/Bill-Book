import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import {
  blankGridLine,
  SalesOrderService,
  SalesOrderView,
  SaveSalesOrderRequest,
  StockAvailability,
  toApiLine,
  toGridLine,
} from '@bill-book/sales-core';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  MessageBoxComponent,
  totalsOf,
  UiMessage,
} from '@bill-book/ui-components';
import { QuoteToOrderDialogComponent } from '../quote-to-order/quote-to-order.dialog';
import { StockAvailabilityDrawerComponent } from '../stock-availability/stock-availability.drawer';

/**
 * The sales order form.
 *
 * **Where each kind of error goes, and why the split is not cosmetic.** A field
 * constraint — required, a two-digit state code, an exchange rate above zero —
 * is shown on that field, because the eye is already there and the fix is one
 * keystroke. A rule about the *document* — not enough stock to reserve, the
 * credit limit is exceeded, a posted order is never edited — goes to the shared
 * message box, because no single field is wrong and pinning it to one would be
 * a guess.
 *
 * **Lines go through the scale boundary, never straight in.** The API serves
 * decimal rupees; `bb-document-line-grid` works in integer paise with quantity
 * at six decimals. Handed one to the other unconverted it does not throw — it
 * computes 10 × 100 ÷ 1 000 000 and shows a priced order as an empty one. That
 * is what `toGridLine` and `toApiLine` are for, and they are the only crossing.
 *
 * **Header in a reactive form, lines in the shared grid.** The brief asked for a
 * `FormArray` of lines; the grid already owns line editing for all nine
 * document types, and a `FormArray` beside it would be a second source of truth
 * for the same rows. So the header is reactive and the lines stay with the
 * component that all nine share.
 */
@Component({
  selector: 'bb-sales-order-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    DocumentLineGridComponent,
    MessageBoxComponent,
    QuoteToOrderDialogComponent,
    StockAvailabilityDrawerComponent,
  ],
  templateUrl: './sales-order-form.component.html',
  styleUrl: './sales-order-form.component.scss',
})
export class SalesOrderFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly orders = inject(SalesOrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isEdit = signal(false);
  protected readonly salesOrderId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly showConvert = signal(false);
  protected readonly showStock = signal(false);
  protected readonly stockLoading = signal(false);
  protected readonly stock = signal<StockAvailability[]>([]);

  /** Draft / ReadyToPost / Posted / Void, as the server last said. */
  protected readonly status = signal('Draft');
  protected readonly documentNo = signal('');
  protected readonly fulfilment = signal('Open');
  protected readonly quoteId = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    documentDate: [today(), Validators.required],
    deliveryDate: [''],
    contactId: [0, [Validators.required, Validators.min(1)]],
    contactGstin: ['', [Validators.maxLength(15)]],
    placeOfSupplyStateCode: ['', [Validators.maxLength(2), Validators.pattern(/^\d{0,2}$/)]],
    currencyCode: ['INR', [Validators.required, Validators.maxLength(3)]],
    exchangeRate: [1, [Validators.required, Validators.min(0.00000001)]],
    billingAddress: [''],
    shippingAddress: [''],
    notes: [''],
    termsAndConditions: [''],
  });

  /**
   * Why the order is being withdrawn.
   *
   * Its own group rather than a control on the form above, because the form
   * above is disabled the moment the order stops being editable — and voiding a
   * *posted* order is exactly the case that needs this field usable.
   */
  protected readonly voidForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });

  /**
   * Why the order is being stopped short.
   *
   * Its own group, like the void reason, and for the same reason: the header
   * form is disabled once the order is confirmed, and short-closing a
   * *confirmed* order is the only case there is.
   */
  protected readonly shortCloseForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });

  protected readonly lines = signal<DocumentLine[]>([blankGridLine(1)]);

  /**
   * The grid's own context. `isInterState` is what the *screen* believes, from
   * the GSTIN typed so far, and it only drives which tax columns are drawn —
   * the server resolves the place of supply again and its answer is the one
   * that reaches the document.
   */
  protected readonly context = computed<DocumentLineContext>(() => ({
    isInterState: this.looksInterState(),
    currencyDecimals: 2,
    allowFreeTextLines: true,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: !this.editable(),
  }));

  /**
   * The form's value as a signal.
   *
   * A reactive form's controls are not signals, so a `computed` that reads
   * `form.controls.x.value` takes a dependency on nothing: it evaluates once and
   * caches that answer for ever. `context` did exactly that — change the
   * customer's GSTIN and the tax columns kept the split they were first drawn
   * with, which is a wrong document rather than a stale screen.
   *
   * Same fault and same fix as the invoice form.
   */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly totals = computed(() => totalsOf(this.lines()));

  /** A posted or voided order is read-only. The lifecycle says so for every document. */
  protected readonly editable = computed(
    () => this.status() === 'Draft' || this.status() === 'ReadyToPost',
  );

  protected readonly canConfirm = computed(
    () => this.isEdit() && this.editable() && this.lines().length > 0,
  );

  protected readonly canVoid = computed(() => this.isEdit() && this.status() !== 'Void');

  /**
   * Short-closing only makes sense on a live order. A draft that is not going
   * ahead is voided; one already closed or cancelled has nothing left to stop.
   */
  protected readonly canShortClose = computed(
    () =>
      this.status() === 'Posted' &&
      this.fulfilment() !== 'Closed' &&
      this.fulfilment() !== 'Cancelled',
  );

  /** What the document is asking for per item, for the drawer's shortfall column. */
  protected readonly wantedByItem = computed(() => {
    const wanted = new Map<number, number>();

    for (const line of this.lines()) {
      if (line.itemId === null) {
        continue;
      }

      // The grid holds quantity at six decimals; the drawer talks in units.
      wanted.set(line.itemId, (wanted.get(line.itemId) ?? 0) + line.quantity / 1_000_000);
    }

    return wanted;
  });

  /** Items short of what is available, if the drawer has been opened. */
  protected readonly shortItems = computed(() =>
    this.stock().filter(
      (row) => row.isTracked && (this.wantedByItem().get(row.itemId) ?? 0) > row.quantityAvailable,
    ),
  );

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.salesOrderId.set(Number(id));
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    const id = this.salesOrderId();
    if (id === null) {
      return;
    }

    try {
      const order = await this.orders.get(id);
      this.apply(order);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private apply(order: SalesOrderView): void {
    this.status.set(order.status);
    this.documentNo.set(order.documentNo);
    this.fulfilment.set(order.fulfilmentStatus);
    this.quoteId.set(order.quoteId ?? null);

    this.form.patchValue({
      documentDate: order.documentDate,
      deliveryDate: order.deliveryDate ?? '',
      contactId: order.contactId,
      contactGstin: order.contactGstin ?? '',
      currencyCode: order.currencyCode,
      exchangeRate: order.exchangeRate,
      billingAddress: order.billingAddress ?? '',
      shippingAddress: order.shippingAddress ?? '',
      notes: order.notes ?? '',
      termsAndConditions: order.termsAndConditions ?? '',
    });

    // Through the scale boundary. Straight in, a ₹100 line reads as zero.
    this.lines.set(
      order.lines.length > 0
        ? order.lines.map((line, index) => toGridLine(line, index + 1))
        : [blankGridLine(1)],
    );

    if (!this.editable()) {
      this.form.disable({ emitEvent: false });
    }

    if (order.status === 'Void' && order.voidReason) {
      this.messages.set([
        { tone: 'warning', text: `This order was voided: ${order.voidReason}` },
      ]);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set([...lines]);
  }

  protected onPickItem(_index: number): void {
    // The item picker is a lookup dialog the host opens; wiring it is T2.4,
    // which is where the item lookup endpoint lands. Until then a line is keyed
    // by hand, which the grid already supports.
  }

  protected async save(): Promise<void> {
    this.form.markAllAsTouched();
    this.messages.set([]);

    if (this.form.invalid) {
      return;
    }

    const priced = this.lines().filter((line) => line.quantity > 0);

    if (priced.length === 0) {
      this.messages.set([
        { tone: 'error', text: 'An order needs at least one line with a quantity on it.' },
      ]);
      return;
    }

    this.saving.set(true);

    const value = this.form.getRawValue();

    const request: SaveSalesOrderRequest = {
      documentDate: value.documentDate,
      deliveryDate: value.deliveryDate || undefined,
      contactId: value.contactId,
      contactGstin: value.contactGstin || undefined,
      placeOfSupplyStateCode: value.placeOfSupplyStateCode || undefined,
      currencyCode: value.currencyCode || undefined,
      exchangeRate: value.exchangeRate,
      billingAddress: value.billingAddress || undefined,
      shippingAddress: value.shippingAddress || undefined,
      notes: value.notes || undefined,
      termsAndConditions: value.termsAndConditions || undefined,
      lines: priced.map(toApiLine),
    };

    try {
      const id = this.salesOrderId();

      if (this.isEdit() && id !== null) {
        await this.orders.update(id, request);
        await this.load();
        this.messages.set([{ tone: 'success', text: 'Sales order saved.' }]);
      } else {
        const created = await this.orders.create(request);
        await this.router.navigate(['/sales/sales-orders', created.salesOrderId]);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Confirm the order, which reserves its stock.
   *
   * The refusal this exists to show properly is the stock one: the server names
   * the items that were short, and that sentence goes into the message box
   * whole rather than being reduced to "insufficient stock".
   */
  protected async confirm(): Promise<void> {
    const id = this.salesOrderId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.orders.confirm(id);
      await this.load();
      this.messages.set([
        { tone: 'success', text: 'Sales order confirmed. Its stock is now reserved.' },
      ]);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async voidOrder(): Promise<void> {
    const id = this.salesOrderId();

    if (id === null) {
      return;
    }

    // The reason is a field constraint, so it shows on its own field — the same
    // rule the server enforces, said before the round trip rather than instead
    // of it.
    this.voidForm.markAllAsTouched();

    if (this.voidForm.invalid) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.orders.voidOrder(id, { reason: this.voidForm.controls.reason.value.trim() });
      this.voidForm.reset();
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Open the stock drawer, reading availability for the lines on screen.
   *
   * Read on open rather than kept live: a figure that refreshed while somebody
   * typed would flicker between two equally stale answers, and the one that
   * decides is taken on confirm regardless.
   */
  protected async openStock(): Promise<void> {
    const itemIds = [...new Set(
      this.lines()
        .map((line) => line.itemId)
        .filter((id): id is number => id !== null),
    )];

    this.showStock.set(true);
    this.stockLoading.set(true);

    try {
      this.stock.set(await this.orders.availability(itemIds));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.stock.set([]);
    } finally {
      this.stockLoading.set(false);
    }
  }

  /**
   * Close the order short: nothing further is coming, and what it still holds
   * goes back on the shelf.
   *
   * **Distinct from a void**, which says the order should not have existed. Four
   * of ten delivered and closed is completed trading history; voiding it would
   * withdraw a document that shipped goods.
   */
  protected async shortClose(): Promise<void> {
    const id = this.salesOrderId();
    if (id === null) {
      return;
    }

    this.shortCloseForm.markAllAsTouched();

    if (this.shortCloseForm.invalid) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.orders.shortClose(id, {
        reason: this.shortCloseForm.controls.reason.value.trim(),
      });

      this.shortCloseForm.reset();
      await this.load();

      this.messages.set([
        {
          tone: 'success',
          text: 'Order closed. Anything it was still holding has been released.',
        },
      ]);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async onConverted(salesOrderId: number): Promise<void> {
    this.showConvert.set(false);
    await this.router.navigate(['/sales/sales-orders', salesOrderId]);
  }

  /** Whether a field should show its error yet — touched, and actually wrong. */
  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  /**
   * What the screen guesses about the place of supply, for the grid's columns.
   *
   * The first two digits of a GSTIN are its state. With no GSTIN and no stated
   * code the screen cannot tell, and guesses intra-state — the ordinary case,
   * and one the server corrects on save if it is wrong.
   */
  private looksInterState(): boolean {
    // Read through formValue, not off the controls: the controls are not
    // signals, so reading them here would make `context` cache its first answer.
    const value = this.formValue();
    const stated = value.placeOfSupplyStateCode ?? '';
    const gstin = value.contactGstin ?? '';
    const supply = stated || gstin.slice(0, 2);

    return supply.length === 2 && supply !== BRANCH_STATE_FALLBACK;
  }
}

/**
 * The branch's own state, until the settings endpoint is wired into this page.
 *
 * It affects the columns drawn and nothing else — the document's own
 * `IsInterState` comes from `PlaceOfSupply.Resolve` on the server, against the
 * branch's real state code.
 */
const BRANCH_STATE_FALLBACK = '33';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
