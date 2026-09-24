import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import {
  CreditNoteReason,
  CreditNoteService,
  CreditNoteView,
  InvoiceService,
  LedgerService,
  OutstandingBalance,
  SaveCreditNoteRequest,
  toApiLine,
  toGridLine,
  SalesLookupService,
} from '@bill-book/sales-core';
import { SalesPicker } from '../sales-picker';
import {
  AllocationGridComponent,
  AllocationRow,
  BbSelectOption,
  DateInputComponent,
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  ExchangeRateInputComponent,
  FormFieldComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextareaComponent,
  TextInputComponent,
  totalsOf,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * A grid line that remembers which invoice line it corrects. The grid spreads a
 * line into every copy it makes, so the field rides along through edits.
 */
type CreditNoteGridLine = DocumentLine & { invoiceDetailId?: number | null };

/**
 * The credit note form — the correction of a posted invoice.
 *
 * **The lines come from the invoice.** *Load invoice* fills the customer and one
 * line per invoice line — for a sales return, at what is still left to come
 * back; for anything else, at the quantity invoiced, for the price to be
 * corrected. Each line carries the invoice line it corrects, which is what the
 * server checks it against and what a returned item's cost is taken from.
 *
 * **Posting is irreversible for a return.** The goods go back into stock, so a
 * posted sales return cannot be voided; a posted price correction, discount or
 * deficiency can, and its entry is withdrawn.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-credit-note-form',
  standalone: true,
  imports: [
    FormFieldComponent,
    LookupDialogComponent,
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    DocumentLineGridComponent,
    AllocationGridComponent,
    MessageBoxComponent,
    DateInputComponent,
    TextInputComponent,
    TextareaComponent,
    NumberInputComponent,
    SelectComponent,
    ExchangeRateInputComponent,
  ],
  templateUrl: './credit-note-form.component.html',
  styleUrl: './credit-note-form.component.scss',
})
export class CreditNoteFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly creditNotes = inject(CreditNoteService);
  private readonly invoices = inject(InvoiceService);
  private readonly ledger = inject(LedgerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isEdit = signal(false);
  protected readonly creditNoteId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly loadingInvoice = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly status = signal('Draft');
  protected readonly documentNo = signal('');

  protected readonly form = this.fb.nonNullable.group({
    documentDate: [today(), Validators.required],
    invoiceId: [null as number | null, [Validators.required, Validators.min(1)]],
    contactId: [0, [Validators.required, Validators.min(1)]],
    contactGstin: ['', [Validators.maxLength(15)]],
    placeOfSupplyStateCode: ['', [Validators.maxLength(2), Validators.pattern(/^\d{0,2}$/)]],
    reasonCode: [CreditNoteReason.SalesReturn, Validators.required],
    currencyCode: ['INR', [Validators.required, Validators.maxLength(3)]],
    exchangeRate: [1, [Validators.required, Validators.min(0.00000001)]],
    billingAddress: ['', [Validators.maxLength(100)]],
    shippingAddress: ['', [Validators.maxLength(100)]],
    notes: ['', [Validators.maxLength(500)]],
  });

  /** Why the note is being withdrawn. Its own group, so it stays usable on a posted note. */
  protected readonly voidForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });

  /** The server's reasons, value for value. Only a sales return brings goods back. */
  protected readonly reasonCodes: BbSelectOption<CreditNoteReason>[] = [
    { value: CreditNoteReason.SalesReturn, label: 'Sales return' },
    { value: CreditNoteReason.PriceCorrection, label: 'Price correction' },
    { value: CreditNoteReason.PostSaleDiscount, label: 'Discount after sale' },
    { value: CreditNoteReason.Deficiency, label: 'Deficiency or damage' },
    { value: CreditNoteReason.Cancellation, label: 'Cancellation of the invoice' },
  ];

  protected readonly lines = signal<CreditNoteGridLine[]>([]);

  /** The chosen customer as it reads on the form; the id itself is the `contactId` control. */
  protected readonly contactLabel = signal('');

  /** The customer and item pickers (TK-15). */
  protected readonly picker = new SalesPicker(inject(SalesLookupService), {
    customer: (row) => this.chooseCustomer(row),
    item: () => undefined,
  });
  protected readonly allocationRows = signal<AllocationRow[]>([]);

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

  /** What the note will claim, in rupees — the unit the outstanding balances speak. */
  protected readonly amountToAllocate = computed(() => this.totals().totalAmount / 100);

  protected readonly editable = computed(
    () => this.status() === 'Draft' || this.status() === 'ReadyToPost',
  );

  protected readonly canPost = computed(
    () => this.isEdit() && this.editable() && this.lines().length > 0,
  );

  /** A draft, or a posted note that brought no goods back. */
  protected readonly canVoid = computed(() => {
    if (!this.isEdit() || this.status() === 'Void') {
      return false;
    }

    const isReturn = Number(this.formValue().reasonCode) === CreditNoteReason.SalesReturn;
    return this.editable() || !isReturn;
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.creditNoteId.set(Number(id));
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    const id = this.creditNoteId();
    if (id === null) {
      return;
    }

    try {
      this.apply(await this.creditNotes.get(id));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private apply(note: CreditNoteView): void {
    this.status.set(note.status);
    this.documentNo.set(note.documentNo);

    this.contactLabel.set(SalesPicker.savedLabel(null, note.contactName, note.contactId));

    this.form.patchValue({
      documentDate: note.documentDate,
      invoiceId: note.invoiceId,
      contactId: note.contactId,
      contactGstin: note.contactGstin ?? '',
      reasonCode: note.reasonCode,
      currencyCode: note.currencyCode,
      exchangeRate: note.exchangeRate,
      billingAddress: note.billingAddress ?? '',
      shippingAddress: note.shippingAddress ?? '',
      notes: note.notes ?? '',
    });

    this.lines.set(
      note.lines.map((line, index) => ({
        ...toGridLine({ ...line, itemName: line.itemLabel }, index + 1),
        invoiceDetailId: line.invoiceDetailId,
      })),
    );

    if (this.editable()) {
      this.form.enable({ emitEvent: false });
    } else {
      this.form.disable({ emitEvent: false });
    }

    if (note.status === 'Void' && note.voidReason) {
      this.messages.set([{ tone: 'warning', text: `This credit note was voided: ${note.voidReason}` }]);
    }
  }

  /**
   * Fills the note from a posted invoice: its customer, addresses and currency,
   * and one line per invoice line. The server checks every line against the
   * invoice again on save and on post.
   */
  protected async loadInvoice(): Promise<void> {
    const invoiceId = this.form.controls.invoiceId.value;
    this.messages.set([]);

    if (!invoiceId || invoiceId < 1) {
      this.form.controls.invoiceId.markAsTouched();
      this.messages.set([{ tone: 'error', text: 'Enter the number of a posted invoice.' }]);
      return;
    }

    this.loadingInvoice.set(true);

    try {
      const invoice = await this.invoices.get(invoiceId);

      if (invoice.status !== 'Posted') {
        this.messages.set([{ tone: 'error', text: 'A credit note can only correct a posted invoice.' }]);
        return;
      }

      const isReturn = Number(this.form.controls.reasonCode.value) === CreditNoteReason.SalesReturn;

      const available = invoice.lines
        .map((line) => ({
          line,
          quantity: isReturn ? (line.quantity ?? 0) - line.returnedQuantity : (line.quantity ?? 0),
        }))
        .filter(({ quantity }) => quantity > 0);

      if (available.length === 0) {
        this.messages.set([
          { tone: 'warning', text: 'Everything on this invoice has already been returned.' },
        ]);
        return;
      }

      this.contactLabel.set(SalesPicker.savedLabel(invoice.contactCode, invoice.contactName, invoice.contactId));

      this.form.patchValue({
        contactId: invoice.contactId,
        contactGstin: invoice.contactGstin ?? '',
        currencyCode: invoice.currencyCode,
        exchangeRate: invoice.exchangeRate,
        billingAddress: invoice.billingAddress ?? '',
        shippingAddress: invoice.shippingAddress ?? '',
      });

      this.lines.set(
        available.map(({ line, quantity }, index) => ({
          ...toGridLine({ ...line, itemName: line.itemLabel, quantity, discountAmount: 0 }, index + 1),
          invoiceDetailId: line.invoiceDetailId,
        })),
      );

      await this.loadOutstanding();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loadingInvoice.set(false);
    }
  }

  /**
   * The customer's invoices with something left to claim, shown so the note's
   * invoice can be picked from them. A payment's negative balance is not an
   * invoice, and a settled invoice owes nothing.
   */
  protected async loadOutstanding(): Promise<void> {
    const contactId = this.form.controls.contactId.value;
    if (!contactId || contactId <= 0) {
      this.allocationRows.set([]);
      return;
    }

    try {
      const balances = await firstValueFrom(this.ledger.outstandingBalances(contactId));
      this.allocationRows.set(
        balances
          .filter((b) => b.transactionTypeCode === 'INV' && b.outstandingAmount > 0)
          .map((b) => toAllocationRow(b)),
      );
    } catch {
      // An advisory list: the note can still be keyed by invoice number.
      this.allocationRows.set([]);
    }
  }

  /**
   * The grid picks the invoice. A credit note corrects exactly one — GST
   * requires it — so a single allocated row loads that invoice, and two of them
   * are refused here rather than at the ledger.
   */
  protected async onAllocationRowsChange(rows: AllocationRow[]): Promise<void> {
    this.allocationRows.set(rows);

    const allocated = rows.filter((r) => (r.allocatedAmount || 0) > 0);

    if (allocated.length > 1) {
      this.messages.set([
        { tone: 'error', text: 'A credit note corrects exactly one invoice. Pick a single invoice.' },
      ]);
      return;
    }

    if (allocated.length === 1 && allocated[0].transactionId !== this.form.controls.invoiceId.value) {
      this.form.patchValue({ invoiceId: allocated[0].transactionId });
      await this.loadInvoice();
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set([...lines]);
  }

  protected onPickItem(_index: number): void {
    // Lines come from the invoice; there is nothing to pick.
  }

  protected openCustomerPicker(): void {
    if (this.form.controls.contactId.disabled) {
      return;
    }

    void this.picker.openCustomer();
  }

  /**
   * The customer, chosen by name. Their GSTIN fills the field when it is still
   * empty — the one the user typed wins — because the GSTIN is what decides
   * intra- against inter-state tax.
   */
  private chooseCustomer(row: LookupRow): void {
    this.contactLabel.set(SalesPicker.label(row));
    this.form.controls.contactId.setValue(row.id);
    this.form.controls.contactId.markAsTouched();

    if (row.meta && !this.form.controls.contactGstin.value) {
      this.form.controls.contactGstin.setValue(row.meta);
    }

    void this.loadOutstanding();
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
        { tone: 'error', text: 'Load the invoice first — a credit note needs at least one of its lines.' },
      ]);
      return;
    }

    if (priced.some((line) => !line.invoiceDetailId)) {
      this.messages.set([
        {
          tone: 'error',
          text: 'Every line must be one of the invoice’s lines. Load the invoice again rather than adding lines by hand.',
        },
      ]);
      return;
    }

    this.saving.set(true);

    const value = this.form.getRawValue();

    const request: SaveCreditNoteRequest = {
      invoiceId: value.invoiceId!,
      documentDate: value.documentDate,
      contactId: value.contactId,
      contactGstin: value.contactGstin || undefined,
      placeOfSupplyStateCode: value.placeOfSupplyStateCode || undefined,
      reasonCode: Number(value.reasonCode) as CreditNoteReason,
      currencyCode: value.currencyCode || undefined,
      exchangeRate: value.exchangeRate,
      billingAddress: value.billingAddress || undefined,
      shippingAddress: value.shippingAddress || undefined,
      notes: value.notes || undefined,
      lines: priced.map((line) => {
        const api = toApiLine(line);
        return {
          invoiceDetailId: line.invoiceDetailId!,
          itemId: api.itemId ?? undefined,
          quantity: api.quantity ?? 0,
          unitPrice: api.unitPrice ?? 0,
          discountPercent: api.discountPercent ?? 0,
          taxGroupId: api.taxGroupId ?? undefined,
        };
      }),
    };

    try {
      const id = this.creditNoteId();

      if (this.isEdit() && id !== null) {
        await this.creditNotes.update(id, request);
        await this.load();
        this.messages.set([{ tone: 'success', text: 'Credit note saved.' }]);
      } else {
        const created = await this.creditNotes.create(request);
        await this.router.navigate(['/sales/credit-notes', created.creditNoteId]);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /** Claims the note against its invoice, returns any goods, and reverses the revenue and tax. */
  protected async post(): Promise<void> {
    const id = this.creditNoteId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.creditNotes.post(id);
      await this.load();
      this.messages.set([{ tone: 'success', text: 'Credit note posted.' }]);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async voidCreditNote(): Promise<void> {
    const id = this.creditNoteId();
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
      await this.creditNotes.voidCreditNote(id, { reason: this.voidForm.controls.reason.value.trim() });
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

  /** Which tax columns to draw. The note's own `IsInterState` is decided on the server. */
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

function toAllocationRow(b: OutstandingBalance): AllocationRow {
  return {
    transactionTypeCode: b.transactionTypeCode,
    transactionId: b.transactionId,
    documentNo: b.documentNo,
    documentDate: b.documentDate,
    dueDate: b.dueDate,
    totalAmount: b.totalAmount,
    outstandingAmount: b.outstandingAmount,
    allocatedAmount: 0,
  };
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
