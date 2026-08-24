import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import {
  blankGridLine,
  GlPreviewResult,
  InvoiceService,
  InvoiceView,
  SaveInvoiceRequest,
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
import { OrderToInvoiceDialogComponent } from '../order-to-invoice/order-to-invoice.dialog';

/**
 * The invoice form.
 *
 * **This is the document the books and the GST return are built from**, which is
 * what makes two of its behaviours non-negotiable:
 *
 * - **Posting is irreversible and the screen says so before it happens.** It
 *   writes the double entry, issues the stock and freezes the invoice. The GL
 *   preview beside the button shows exactly which accounts it will move and in
 *   which direction, read from the server rather than guessed at here — there is
 *   one implementation of that posting and this is not a second one.
 * - **A posted invoice is never edited.** It is corrected with a credit note, or
 *   voided and replaced. The form goes read-only on post, and the lifecycle
 *   refuses it server-side regardless of what this screen allows.
 *
 * Errors split the same way as everywhere else: a field constraint shows on its
 * field, a rule about the document goes to the shared message box with the
 * server's own words.
 *
 * Lines cross the paise/rupee boundary through `toGridLine` / `toApiLine`.
 * Handed straight through they do not throw — they compute a priced invoice as
 * an empty one.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    DocumentLineGridComponent,
    MessageBoxComponent,
    OrderToInvoiceDialogComponent,
  ],
  templateUrl: './invoice-form.component.html',
  styleUrl: './invoice-form.component.scss',
})
export class InvoiceFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly invoices = inject(InvoiceService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly isEdit = signal(false);
  protected readonly invoiceId = signal<number | null>(null);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly showConvert = signal(false);
  protected readonly glPreview = signal<GlPreviewResult | null>(null);

  protected readonly status = signal('Draft');
  protected readonly documentNo = signal('');
  protected readonly salesOrderId = signal<number | null>(null);
  protected readonly daysOverdue = signal(0);

  protected readonly form = this.fb.nonNullable.group({
    documentDate: [today(), Validators.required],
    dueDate: [inDays(30), Validators.required],
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
   * Why the invoice is being withdrawn.
   *
   * Its own group rather than a control on the form above, because that form is
   * disabled the moment the invoice is posted — and voiding a *posted* invoice
   * is exactly the case that needs this field usable.
   */
  protected readonly voidForm = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(300)]],
  });

  protected readonly lines = signal<DocumentLine[]>([blankGridLine(1)]);

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
   * with, which is a wrong invoice rather than a stale screen.
   */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });

  protected readonly totals = computed(() => totalsOf(this.lines()));

  /** A posted or voided invoice is read-only. The lifecycle says so for every document. */
  protected readonly editable = computed(
    () => this.status() === 'Draft' || this.status() === 'ReadyToPost',
  );

  protected readonly canPost = computed(
    () => this.isEdit() && this.editable() && this.lines().length > 0,
  );

  protected readonly canVoid = computed(() => this.isEdit() && this.status() !== 'Void');

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (id && id !== 'new') {
      this.isEdit.set(true);
      this.invoiceId.set(Number(id));
      void this.load();
    }
  }

  protected async load(): Promise<void> {
    const id = this.invoiceId();
    if (id === null) {
      return;
    }

    try {
      const invoice = await this.invoices.get(id);
      this.apply(invoice);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  private apply(invoice: InvoiceView): void {
    this.status.set(invoice.status);
    this.documentNo.set(invoice.documentNo);
    this.salesOrderId.set(invoice.salesOrderId ?? null);
    this.daysOverdue.set(invoice.daysOverdue ?? 0);

    this.form.patchValue({
      documentDate: invoice.documentDate,
      dueDate: invoice.dueDate ?? '',
      contactId: invoice.contactId,
      contactGstin: invoice.contactGstin ?? '',
      currencyCode: invoice.currencyCode,
      exchangeRate: invoice.exchangeRate,
      billingAddress: invoice.billingAddress ?? '',
      shippingAddress: invoice.shippingAddress ?? '',
      notes: invoice.notes ?? '',
      termsAndConditions: invoice.termsAndConditions ?? '',
    });

    // Through the scale boundary. Straight in, a ₹100 line reads as zero.
    this.lines.set(
      invoice.lines.length > 0
        ? invoice.lines.map((line, index) => toGridLine(line, index + 1))
        : [blankGridLine(1)],
    );

    if (!this.editable()) {
      this.form.disable({ emitEvent: false });
    }

    if (invoice.status === 'Void' && invoice.voidReason) {
      this.messages.set([
        { tone: 'warning', text: `This invoice was voided: ${invoice.voidReason}` },
      ]);
    } else if (invoice.status === 'Posted' && (invoice.daysOverdue ?? 0) > 0) {
      this.messages.set([
        {
          tone: 'warning',
          text: `This invoice is ${invoice.daysOverdue} days past its due date.`,
        },
      ]);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set([...lines]);
  }

  protected onPickItem(_index: number): void {
    // The item picker is a lookup dialog the host opens; wiring it waits on the
    // item lookup endpoint. Until then a line is keyed by hand, which the grid
    // already supports.
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
        { tone: 'error', text: 'An invoice needs at least one line with a quantity on it.' },
      ]);
      return;
    }

    this.saving.set(true);

    const value = this.form.getRawValue();

    const request: SaveInvoiceRequest = {
      documentDate: value.documentDate,
      dueDate: value.dueDate || undefined,
      contactId: value.contactId,
      salesOrderId: this.salesOrderId() ?? undefined,
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
      const id = this.invoiceId();

      if (this.isEdit() && id !== null) {
        await this.invoices.update(id, request);
        await this.load();
        this.messages.set([{ tone: 'success', text: 'Invoice saved.' }]);
      } else {
        const created = await this.invoices.create(request);
        await this.router.navigate(['/sales/invoices', created.invoiceId]);
      }
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * What posting would write to the ledger, read from the server.
   *
   * Shown before the irreversible step rather than after it, and computed by the
   * same code that will do the posting — a preview drawn from a second
   * implementation would eventually disagree with the entry it claims to predict.
   */
  protected async preview(): Promise<void> {
    const id = this.invoiceId();
    if (id === null) {
      return;
    }

    this.messages.set([]);

    try {
      this.glPreview.set(await this.invoices.previewGl(id));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    }
  }

  /** Posts the double entry, issues the stock, and freezes the invoice. */
  protected async post(): Promise<void> {
    const id = this.invoiceId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    try {
      await this.invoices.post(id);
      this.glPreview.set(null);
      await this.load();
      this.messages.set([
        {
          tone: 'success',
          text: 'Invoice posted. The entry is in the ledger and the stock has been issued.',
        },
      ]);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async voidInvoice(): Promise<void> {
    const id = this.invoiceId();
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
      await this.invoices.voidInvoice(id, { reason: this.voidForm.controls.reason.value.trim() });
      this.voidForm.reset();
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected async onConverted(invoiceId: number): Promise<void> {
    this.showConvert.set(false);
    await this.router.navigate(['/sales/invoices', invoiceId]);
  }

  /** Whether a field should show its error yet — touched, and actually wrong. */
  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  /**
   * What the screen guesses about the place of supply, for the grid's columns.
   *
   * The first two digits of a GSTIN are its state. The document's own
   * `IsInterState` comes from `PlaceOfSupply.Resolve` on the server, against the
   * branch's real state code; this only decides which columns are drawn.
   */
  private looksInterState(): boolean {
    // Read through the signal, not the controls, or this never recomputes.
    const value = this.formValue();
    const stated = value.placeOfSupplyStateCode ?? '';
    const gstin = value.contactGstin ?? '';
    const supply = stated || gstin.slice(0, 2);

    return supply.length === 2 && supply !== BRANCH_STATE_FALLBACK;
  }
}

/**
 * The branch's own state, until the settings endpoint is wired into this page.
 * It affects the columns drawn and nothing else.
 */
const BRANCH_STATE_FALLBACK = '33';

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function inDays(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return date.toISOString().slice(0, 10);
}

