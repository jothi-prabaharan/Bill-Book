import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { InvoiceService, SalesOrderListItem, SalesOrderService } from '@bill-book/sales-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/** How many confirmed orders to offer. Enough to pick from without a second pager. */
const CANDIDATE_PAGE = 100;

/**
 * Turning a confirmed sales order into an invoice.
 *
 * **It sends the order's id and nothing else of the order.** The lines are read
 * server-side and recomputed at the rates in force on the invoice's own date — a
 * dialog that posted the lines it happened to be showing could raise an invoice
 * that claims to come from an order it does not match, and this is the document
 * a GST return is filed from.
 *
 * Only orders that have been **confirmed** and not already invoiced are offered.
 * An unconfirmed order is holding no stock, so invoicing it would issue goods
 * nobody reserved; the server refuses that too, and says why.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-order-to-invoice-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MessageBoxComponent],
  templateUrl: './order-to-invoice.dialog.html',
  styleUrl: './order-to-invoice.dialog.scss',
})
export class OrderToInvoiceDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly orders = inject(SalesOrderService);
  private readonly invoices = inject(InvoiceService);

  /** The new invoice's id. The host navigates; this dialog does not. */
  readonly converted = output<number>();

  readonly cancelled = output<void>();

  protected readonly candidates = signal<SalesOrderListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    salesOrderId: [0, [Validators.required, Validators.min(1)]],
    documentDate: [today(), Validators.required],

    // Required, unlike the quote-to-order step. An invoice without a due date is
    // refused by the server, and an order has no due date to carry across — a
    // delivery date is when goods are expected, not when money is.
    dueDate: [inDays(30), Validators.required],
    placeOfSupplyStateCode: ['', [Validators.maxLength(2), Validators.pattern(/^\d{0,2}$/)]],
    notes: [''],
  });

  private readonly selectedId = signal(0);

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return this.candidates().find((o) => o.salesOrderId === id) ?? null;
  });

  protected readonly hasCandidates = computed(() => this.candidates().length > 0);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);

    try {
      // Posted is what "confirmed" looks like on a sales order, and the server
      // will only convert one in that state.
      const page = await this.orders.list({ skip: 0, take: CANDIDATE_PAGE, status: 'Posted' });

      this.candidates.set(page.rows.filter((o) => !o.invoicedDocumentId));
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loading.set(false);
    }
  }

  protected onSelect(salesOrderId: number): void {
    this.selectedId.set(salesOrderId);
    this.form.patchValue({ salesOrderId });
    this.form.controls.salesOrderId.markAsTouched();
  }

  protected async convert(): Promise<void> {
    // Touched first, so the field errors that were waiting silently appear all
    // at once rather than one per attempt.
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      return;
    }

    this.saving.set(true);
    this.messages.set([]);

    const value = this.form.getRawValue();

    try {
      const result = await this.invoices.createFromSalesOrder(value.salesOrderId, {
        documentDate: value.documentDate,
        dueDate: value.dueDate,
        placeOfSupplyStateCode: value.placeOfSupplyStateCode || undefined,
        notes: value.notes || undefined,
      });

      this.converted.emit(result.invoiceId);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.saving.set(false);
    }
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  /** Whether a field should show its error yet — touched, and actually wrong. */
  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function inDays(days: number): string {
  const date = new Date();
  date.setDate(date.getDate() + days);
  return date.toISOString().slice(0, 10);
}

