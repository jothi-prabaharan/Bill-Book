import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import {
  blankGridLine,
  ChallanType,
  DeliveryChallanService,
  DeliveryChallanView,
  SalesOrderService,
  SaveDeliveryChallanRequest,
  toApiLine,
  toGridLine,
} from '@bill-book/sales-core';
import {
  BbSelectOption,
  DateInputComponent,
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  ExchangeRateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextareaComponent,
  TextInputComponent,
  totalsOf,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * A grid line that remembers which order line it delivers.
 *
 * The grid spreads a line into every copy it makes, so the extra field rides
 * along through edits; a line added by hand simply has none, and the server
 * refuses it on a challan against an order rather than guessing which order
 * line it meant.
 */
type ChallanGridLine = DocumentLine & { salesOrderDetailId?: number | null };

/**
 * The delivery challan form — the document the goods leave on.
 *
 * **Posting dispatches.** It issues the stock and, against an order, moves the
 * order's delivered and reserved quantities. It is irreversible: a dispatched
 * challan cannot be voided, only answered with a return. The form goes
 * read-only on post, and the lifecycle refuses edits server-side regardless.
 *
 * **Against an order, the lines come from the order.** *Load order* fills the
 * customer and one line per order line still outstanding, each carrying the
 * order line it delivers. Lower a quantity to deliver part of it; remove a line
 * to leave it for a later challan.
 *
 * Errors split the same way as on the invoice: a field constraint shows on its
 * field, a rule about the document goes to the message box with the server's
 * own words.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-delivery-challan-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    DocumentLineGridComponent,
    MessageBoxComponent,
    DateInputComponent,
    TextInputComponent,
    TextareaComponent,
    NumberInputComponent,
    SelectComponent,
    ExchangeRateInputComponent,
  ],
  templateUrl: './delivery-challan-form.component.html',
  styleUrl: './delivery-challan-form.component.scss',
})
export class DeliveryChallanFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly challans = inject(DeliveryChallanService);
  private readonly orders = inject(SalesOrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isEdit = signal(false);
  protected readonly challanId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly loadingOrder = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly status = signal('Draft');
  protected readonly documentNo = signal('');

  protected readonly form = this.fb.nonNullable.group({
    documentDate: [today(), Validators.required],
    dispatchDate: [today(), Validators.required],
    salesOrderId: [null as number | null, [Validators.min(1)]],
    contactId: [0, [Validators.required, Validators.min(1)]],
    contactGstin: ['', [Validators.maxLength(15)]],
    placeOfSupplyStateCode: ['', [Validators.maxLength(2), Validators.pattern(/^\d{0,2}$/)]],
    challanType: [ChallanType.Sale, Validators.required],
    vehicleNo: [''],
    transporterName: ['', [Validators.maxLength(100)]],
    ewayBillNo: ['', [Validators.maxLength(12), Validators.pattern(/^\d{0,12}$/)]],
    ewayBillDate: [''],
    currencyCode: ['INR', [Validators.required, Validators.maxLength(3)]],
    exchangeRate: [1, [Validators.required, Validators.min(0.00000001)]],
    billingAddress: ['', [Validators.maxLength(100)]],
    shippingAddress: ['', [Validators.maxLength(100)]],
    notes: ['', [Validators.maxLength(500)]],
  });

  /**
   * Why the challan is being withdrawn. Its own group, like the invoice's,
   * so it stays usable whatever the state of the form above.
   */
  protected readonly voidForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });

  /** What the goods are going out for. Only a sale is a supply; the rest move stock and sell nothing. */
  protected readonly challanTypes: BbSelectOption<ChallanType>[] = [
    { value: ChallanType.Sale, label: 'Sale' },
    { value: ChallanType.JobWork, label: 'Job work' },
    { value: ChallanType.Approval, label: 'On approval' },
    { value: ChallanType.BranchTransfer, label: 'Branch transfer' },
    { value: ChallanType.Sample, label: 'Sample' },
  ];

  protected readonly lines = signal<ChallanGridLine[]>([blankGridLine(1)]);

  /** Read through a signal, or a `computed` over the controls never recomputes. */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly context = computed<DocumentLineContext>(() => ({
    isInterState: this.looksInterState(),
    currencyDecimals: 2,
    allowFreeTextLines: false,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: !this.editable(),
  }));

  protected readonly totals = computed(() => totalsOf(this.lines()));

  protected readonly editable = computed(
    () => this.status() === 'Draft' || this.status() === 'ReadyToPost',
  );

  protected readonly canPost = computed(
    () => this.isEdit() && this.editable() && this.lines().length > 0,
  );

  /** Only a draft: a dispatched challan is answered with a return, not a void. */
  protected readonly canVoid = computed(() => this.isEdit() && this.editable());

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.challanId.set(Number(id));
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    const id = this.challanId();
    if (id === null) {
      return;
    }

    try {
      this.apply(await this.challans.get(id));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private apply(challan: DeliveryChallanView): void {
    this.status.set(challan.status);
    this.documentNo.set(challan.documentNo);

    this.form.patchValue({
      documentDate: challan.documentDate,
      dispatchDate: challan.dispatchDate,
      salesOrderId: challan.salesOrderId ?? null,
      contactId: challan.contactId,
      contactGstin: challan.contactGstin ?? '',
      challanType: challan.challanType,
      vehicleNo: challan.vehicleNo ?? '',
      transporterName: challan.transporterName ?? '',
      ewayBillNo: challan.ewayBillNo ?? '',
      ewayBillDate: challan.ewayBillDate ?? '',
      currencyCode: challan.currencyCode,
      exchangeRate: challan.exchangeRate,
      billingAddress: challan.billingAddress ?? '',
      shippingAddress: challan.shippingAddress ?? '',
      notes: challan.notes ?? '',
    });

    // Through the scale boundary, carrying each line's order line with it.
    this.lines.set(
      challan.lines.length > 0
        ? challan.lines.map((line, index) => ({
            ...toGridLine({ ...line, itemName: line.itemLabel }, index + 1),
            salesOrderDetailId: line.salesOrderDetailId ?? null,
          }))
        : [blankGridLine(1)],
    );

    if (this.editable()) {
      this.form.enable({ emitEvent: false });
    } else {
      this.form.disable({ emitEvent: false });
    }

    if (challan.status === 'Void' && challan.voidReason) {
      this.messages.set([
        { tone: 'warning', text: `This challan was voided: ${challan.voidReason}` },
      ]);
    }
  }

  /**
   * Fills the challan from a confirmed sales order: its customer, addresses and
   * currency, and one line for everything each order line still has to deliver.
   *
   * Nothing is decided here that the server does not check again — the save
   * refuses a line that is not the order's, and the post refuses one that
   * would deliver more than is still outstanding by then.
   */
  protected async loadOrder(): Promise<void> {
    const orderId = this.form.controls.salesOrderId.value;
    this.messages.set([]);

    if (!orderId || orderId < 1) {
      this.form.controls.salesOrderId.markAsTouched();
      this.messages.set([{ tone: 'error', text: 'Enter the number of a confirmed sales order.' }]);
      return;
    }

    this.loadingOrder.set(true);

    try {
      const order = await this.orders.get(orderId);

      if (order.status !== 'Posted') {
        this.messages.set([
          { tone: 'error', text: 'Only a confirmed sales order can be delivered against.' },
        ]);
        return;
      }

      const outstanding = order.lines
        .map((line) => ({ line, left: line.quantity! - line.deliveredQuantity }))
        .filter(({ line, left }) => left > 0 && line.itemId);

      if (outstanding.length === 0) {
        this.messages.set([{ tone: 'warning', text: 'Everything on this order has been delivered.' }]);
        return;
      }

      this.form.patchValue({
        contactId: order.contactId,
        contactGstin: order.contactGstin ?? '',
        currencyCode: order.currencyCode,
        exchangeRate: order.exchangeRate,
        billingAddress: order.billingAddress ?? '',
        shippingAddress: order.shippingAddress ?? '',
      });

      this.lines.set(
        outstanding.map(({ line, left }, index) => ({
          ...toGridLine(
            { ...line, itemName: line.itemLabel, quantity: left, discountAmount: 0 },
            index + 1,
          ),
          salesOrderDetailId: line.salesOrderDetailId,
        })),
      );
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loadingOrder.set(false);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set([...lines]);
  }

  protected onPickItem(_index: number): void {
    // The item picker waits on the item lookup endpoint (TK-17). Until then a
    // line is keyed by item id, which the grid already supports.
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
        { tone: 'error', text: 'A challan needs at least one line with a quantity on it.' },
      ]);
      return;
    }

    if (priced.some((line) => !line.itemId)) {
      this.messages.set([
        { tone: 'error', text: 'Every line on a challan needs an item — it is what leaves the shelf.' },
      ]);
      return;
    }

    this.saving.set(true);

    const value = this.form.getRawValue();

    const request: SaveDeliveryChallanRequest = {
      salesOrderId: value.salesOrderId ?? undefined,
      documentDate: value.documentDate,
      contactId: value.contactId,
      contactGstin: value.contactGstin || undefined,
      placeOfSupplyStateCode: value.placeOfSupplyStateCode || undefined,
      challanType: Number(value.challanType) as ChallanType,
      vehicleNo: value.vehicleNo || undefined,
      transporterName: value.transporterName || undefined,
      ewayBillNo: value.ewayBillNo || undefined,
      ewayBillDate: value.ewayBillDate || undefined,
      dispatchDate: value.dispatchDate,
      currencyCode: value.currencyCode || undefined,
      exchangeRate: value.exchangeRate,
      billingAddress: value.billingAddress || undefined,
      shippingAddress: value.shippingAddress || undefined,
      notes: value.notes || undefined,
      lines: priced.map((line) => ({
        ...toApiLine(line),
        salesOrderDetailId: line.salesOrderDetailId ?? undefined,
      })),
    };

    try {
      const id = this.challanId();

      if (this.isEdit() && id !== null) {
        await this.challans.update(id, request);
        await this.load();
        this.messages.set([{ tone: 'success', text: 'Challan saved.' }]);
      } else {
        const created = await this.challans.create(request);
        await this.router.navigate(['/sales/delivery-challans', created.deliveryChallanId]);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /** Dispatches the goods: issues the stock and, against an order, moves its quantities. */
  protected async post(): Promise<void> {
    const id = this.challanId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.challans.post(id);
      await this.load();
      this.messages.set([
        { tone: 'success', text: 'Challan posted. The goods have been issued from stock.' },
      ]);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async voidChallan(): Promise<void> {
    const id = this.challanId();
    if (id === null) {
      return;
    }

    this.voidForm.markAllAsTouched();

    if (this.voidForm.invalid) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.challans.voidChallan(id, { reason: this.voidForm.controls.reason.value.trim() });
      this.voidForm.reset();
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /** Whether a field should show its error yet — touched, and actually wrong. */
  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  /**
   * Which tax columns to draw. The challan's own `IsInterState` is decided on
   * the server against the branch's real state; this only picks the columns.
   */
  private looksInterState(): boolean {
    const value = this.formValue();
    const stated = value.placeOfSupplyStateCode ?? '';
    const gstin = value.contactGstin ?? '';
    const supply = stated || gstin.slice(0, 2);

    return supply.length === 2 && supply !== BRANCH_STATE_FALLBACK;
  }
}

/** The branch's own state, until the settings endpoint is wired into this page — as on the invoice. */
const BRANCH_STATE_FALLBACK = '33';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
