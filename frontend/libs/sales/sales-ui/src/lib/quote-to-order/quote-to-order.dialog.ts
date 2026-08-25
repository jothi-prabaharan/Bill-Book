import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import { QuoteListItem, QuoteService, SalesOrderService } from '@bill-book/sales-core';
import { MessageBoxComponent, UiMessage } from '@bill-book/ui-components';

/**
 * Turning an accepted quote into a sales order.
 *
 * **It sends the quote's id and nothing else of the quote.** The lines are read
 * server-side and recomputed at the rates in force on the order's own date — a
 * dialog that posted the lines it happened to be showing could raise an order
 * that claims to come from a quote it does not match, and the two documents
 * would disagree for the rest of their lives.
 *
 * Only quotes that have been approved and not already converted are offered.
 * The server refuses the other two cases as well, and says why; this list exists
 * so that refusal is rare rather than routine.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-quote-to-order-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MessageBoxComponent],
  templateUrl: './quote-to-order.dialog.html',
  styleUrl: './quote-to-order.dialog.scss',
})
export class QuoteToOrderDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly quotes = inject(QuoteService);
  private readonly orders = inject(SalesOrderService);

  /** The new order's id. The host navigates; this dialog does not. */
  readonly converted = output<number>();

  readonly cancelled = output<void>();

  protected readonly candidates = signal<QuoteListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    quoteId: [0, [Validators.required, Validators.min(1)]],
    documentDate: [today(), Validators.required],
    deliveryDate: [''],
    placeOfSupplyStateCode: ['', [Validators.maxLength(2), Validators.pattern(/^\d{0,2}$/)]],
    notes: [''],
  });

  protected readonly selected = computed(() => {
    const id = this.selectedId();
    return this.candidates().find((q) => q.quoteId === id) ?? null;
  });

  private readonly selectedId = signal(0);

  protected readonly hasCandidates = computed(() => this.candidates().length > 0);

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);

    try {
      const all = await firstValueFrom(this.quotes.list());

      // Posted is what "the customer accepted it" looks like on a quote, and a
      // quote that already became an order cannot become a second one.
      this.candidates.set(
        all.filter((q) => q.status === 'Posted' && !q.convertedToSalesOrderId),
      );
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.loading.set(false);
    }
  }

  protected onSelect(quoteId: number): void {
    this.selectedId.set(quoteId);
    this.form.patchValue({ quoteId });
    this.form.controls.quoteId.markAsTouched();
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
      const result = await this.orders.createFromQuote(value.quoteId, {
        documentDate: value.documentDate,
        deliveryDate: value.deliveryDate || undefined,
        placeOfSupplyStateCode: value.placeOfSupplyStateCode || undefined,
        notes: value.notes || undefined,
      });

      this.converted.emit(result.salesOrderId);
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

