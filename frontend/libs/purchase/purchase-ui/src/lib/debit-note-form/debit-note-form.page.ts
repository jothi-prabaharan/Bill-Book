import { Component, computed, inject, signal } from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

import { ColumnDef, DataGridComponent, DateInputComponent, LookupDialogComponent, LookupRow, NumberInputComponent, TextInputComponent } from '@bill-book/ui-components';

import {
  BillService,
  DEBIT_NOTE_REASONS,
  DebitNoteService,
  DebitNoteView,
  PurchaseLookupService,
  SaveDebitNoteLineRequest,
  SaveDebitNoteRequest,
} from '@bill-book/purchase-core';

type Picker = 'none' | 'vendor' | 'bill';

/**
 * One line of the note, tied to the bill line it reverses.
 *
 * **Not the shared line grid.** A debit note does not build lines — it picks
 * them off a bill and says how much of each goes back, at the price the bill
 * charged. The grid's job is composing a document from items and prices, and
 * offering it here would invite editing figures that have to match the bill.
 */
interface ReturnLine {
  billDetailId: number;
  itemId: number | null;
  label: string;
  /** What the bill charged, per unit. Fixed. */
  unitPrice: number;
  /** How much of the bill line is still returnable. */
  available: number;
  /** How much this note sends back. Zero leaves the line off. */
  quantity: number;
  taxGroupId: number | null;
  taxTreatment: string;
  lineType: string;
  uomId: number | null;
  conversionFactor: number;
  warehouseId: number | null;
  itemBatchId: number | null;
}

/**
 * Raise a debit note.
 *
 * **The reason decides whether stock moves**, and the form says which is which
 * rather than leaving it to be discovered after posting: only a purchase return
 * sends goods back. A price correction or a post-purchase discount reduces what
 * is owed while the goods stay on the shelf.
 */
@Component({
  selector: 'bb-debit-note-form',
  standalone: true,
  imports: [DataGridComponent, CommonModule, FormsModule, RouterModule, LookupDialogComponent, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './debit-note-form.page.html',
  styleUrl: './debit-note-form.page.scss',
})
export class DebitNoteFormPage {
  private readonly notes = inject(DebitNoteService);
  private readonly bills = inject(BillService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly reasons = DEBIT_NOTE_REASONS;

  protected readonly debitNoteId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');

  protected readonly documentDate = signal(this.today());
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected readonly contactGstin = signal('');
  protected readonly billId = signal<number | null>(null);
  protected readonly billLabel = signal('');
  protected readonly reasonCode = signal('PurchaseReturn');
  protected readonly placeOfSupplyStateCode = signal('');
  protected readonly notes_ = signal('');

  protected readonly lines = signal<readonly ReturnLine[]>([]);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  private searchToken = 0;

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.debitNoteId() === null);

  /** Whether the chosen reason sends goods back. Drives the whole warning. */
  protected readonly movesStock = computed(
    () =>
      this.reasons.find((r) => r.code === this.reasonCode())?.movesStock ?? false,
  );

  protected readonly reasonHint = computed(
    () => this.reasons.find((r) => r.code === this.reasonCode())?.hint ?? '',
  );

  /** Only lines with something on them are sent. */
  protected readonly activeLines = computed(() =>
    this.lines().filter((line) => line.quantity > 0),
  );

  protected readonly total = computed(() =>
    this.activeLines().reduce(
      (sum, line) => sum + line.quantity * line.unitPrice,
      0,
    ),
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id !== null && id !== 'new') {
      void this.load(Number(id));
    }
  }

  protected async load(id: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const view: DebitNoteView = await this.notes.get(id);

      this.debitNoteId.set(view.debitNoteId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.documentDate.set(view.documentDate);
      this.contactId.set(view.contactId);
      this.contactLabel.set(
        view.contactName
          ? `${view.contactCode ?? ''} ${view.contactName}`.trim()
          : String(view.contactId),
      );
      this.contactGstin.set(view.contactGstin ?? '');
      this.billId.set(view.billId);
      this.billLabel.set(view.billNo ?? String(view.billId));
      this.reasonCode.set(view.reasonCode);
      this.notes_.set(view.notes ?? '');

      this.lines.set(
        view.lines.map((line) => ({
          billDetailId: line.billDetailId,
          itemId: line.itemId ?? null,
          label: line.itemLabel ?? line.description ?? `Line ${line.lineNumber}`,
          unitPrice: line.unitPrice,
          available: line.quantity,
          quantity: line.quantity,
          taxGroupId: line.taxGroupId ?? null,
          taxTreatment: line.taxTreatment,
          lineType: line.lineType,
          uomId: line.uomId ?? null,
          conversionFactor: line.conversionFactor,
          warehouseId: line.warehouseId ?? null,
          itemBatchId: line.itemBatchId ?? null,
        })),
      );
    } catch {
      this.error.set('This debit note could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Pickers ----------------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  protected openBillPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('bill');
    void this.runSearch('');
  }

  protected onPickerSearch(term: string): void {
    void this.runSearch(term);
  }

  private async runSearch(term: string): Promise<void> {
    const token = ++this.searchToken;
    this.pickerLoading.set(true);

    try {
      const rows: LookupRow[] =
        this.picker() === 'vendor'
          ? (await this.lookups.vendors(term)).map((vendor) => ({
              id: vendor.contactId,
              code: vendor.contactCode,
              name: vendor.displayName,
              meta: vendor.gstin,
            }))
          : (await this.lookups.creditableBills(term, this.contactId())).map(
              (bill) => ({
                id: bill.billId,
                code: bill.documentNo,
                name: bill.contactName ?? '',
                meta: `their ${bill.vendorBillNo} · ${bill.documentDate}`,
              }),
            );

      if (token === this.searchToken) {
        this.pickerRows.set(rows);
      }
    } catch {
      if (token === this.searchToken) {
        this.pickerRows.set([]);
      }
    } finally {
      if (token === this.searchToken) {
        this.pickerLoading.set(false);
      }
    }
  }

  protected onPickerChoose(row: LookupRow): void {
    if (this.picker() === 'vendor') {
      this.contactId.set(row.id);
      this.contactLabel.set(`${row.code} ${row.name}`.trim());

      if (row.meta && !this.contactGstin()) {
        this.contactGstin.set(row.meta);
      }

      // The bill belonged to the previous vendor.
      this.billId.set(null);
      this.billLabel.set('');
      this.lines.set([]);
    } else {
      this.billId.set(row.id);
      this.billLabel.set(row.code);
      void this.pullBillLines(row.id);
    }

    this.closePicker();
  }

  /**
   * Copies the bill's lines, each showing how much is still returnable.
   *
   * A line already fully returned is left out: offering it would only produce a
   * refusal, and the server counts the running returned quantity precisely so
   * the same goods cannot be credited twice.
   */
  private async pullBillLines(billId: number): Promise<void> {
    try {
      const bill = await this.bills.get(billId);

      const returnable = bill.lines
        .map((line) => ({
          billDetailId: line.billDetailId,
          itemId: line.itemId ?? null,
          label: line.itemLabel ?? line.description ?? `Line ${line.lineNumber}`,
          unitPrice: line.unitPrice,
          available: line.quantity - line.returnedQuantity,
          quantity: 0,
          taxGroupId: line.taxGroupId ?? null,
          taxTreatment: line.taxTreatment,
          lineType: line.lineType,
          uomId: line.uomId ?? null,
          conversionFactor: line.conversionFactor,
          warehouseId: line.warehouseId ?? null,
          itemBatchId: line.itemBatchId ?? null,
        }))
        .filter((line) => line.available > 0);

      if (returnable.length === 0) {
        this.notice.set('Every line on that bill has already been returned in full.');
        this.lines.set([]);
        return;
      }

      this.lines.set(returnable);

      if (!this.contactGstin() && bill.contactGstin) {
        this.contactGstin.set(bill.contactGstin);
      }
    } catch {
      this.error.set('That bill could not be read, so its lines were not copied.');
    }
  }

  protected onQuantityChange(index: number, value: number): void {
    this.lines.set(
      this.lines().map((line, at) =>
        at === index
          ? {
              ...line,
              quantity: Math.min(Math.max(value || 0, 0), line.available),
            }
          : line,
      ),
    );
  }

  /** Returning everything still outstanding, which is the common case. */
  protected returnAll(): void {
    this.lines.set(
      this.lines().map((line) => ({ ...line, quantity: line.available })),
    );
  }

  protected closePicker(): void {
    this.picker.set('none');
    this.pickerRows.set([]);
  }

  protected pickerTitle(): string {
    return this.picker() === 'vendor' ? 'Choose a vendor' : 'Choose a bill';
  }

  protected pickerPlaceholder(): string {
    return this.picker() === 'vendor'
      ? 'Search vendors by code or name'
      : 'Search posted bills';
  }

  protected pickerEmptyText(): string {
    return this.picker() === 'vendor'
      ? 'No vendors match.'
      : 'No posted bills for this vendor. A draft bill owes nothing yet — correct '
        + 'the bill itself instead.';
  }

  // ---- Actions ----------------------------------------------------------

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    const billId = this.billId();

    if (contactId === null || billId === null) {
      this.error.set('Choose the vendor and the bill this corrects.');
      return;
    }

    if (this.activeLines().length === 0) {
      this.error.set('Put a quantity on at least one line.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId, billId);
      const id = this.debitNoteId();

      if (id === null) {
        const created = await this.notes.create(request);
        await this.router.navigate(['../', created.debitNoteId], {
          relativeTo: this.route,
        });
      } else {
        await this.notes.update(id, request);
        await this.load(id);
        this.notice.set('Saved.');
      }
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async post(): Promise<void> {
    const id = this.debitNoteId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.notes.post(id);
      await this.load(id);
      this.notice.set(
        this.movesStock()
          ? 'Posted. The goods have gone back and the vendor is owed less.'
          : 'Posted. The vendor is owed less; the goods stayed.',
      );
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  private toRequest(contactId: number, billId: number): SaveDebitNoteRequest {
    const lines: SaveDebitNoteLineRequest[] = this.activeLines().map((line) => ({
      billDetailId: line.billDetailId,
      itemId: line.itemId,
      quantity: line.quantity,
      uomId: line.uomId,
      conversionFactor: line.conversionFactor,
      unitPrice: line.unitPrice,
      isPriceInclusive: false,
      discountPercent: null,
      discountAmount: 0,
      taxTreatment: line.taxTreatment,
      taxGroupId: line.taxGroupId,
      lineType: line.lineType,
      warehouseId: line.warehouseId,
      itemBatchId: line.itemBatchId,
      lineNotes: null,
    }));

    return {
      documentDate: this.documentDate(),
      contactId,
      billId,
      reasonCode: this.reasonCode(),
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      notes: this.notes_() || null,
      lines,
    };
  }

  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The debit note could not be saved.';
  }

  columns: ColumnDef[] = [
    { field: 'label', header: 'Item' },
    { field: 'available', header: 'Available' },
    { field: 'quantity', header: 'Quantity' },
    { field: 'unitPrice', header: 'Price' },
    { field: 'value', header: 'Value' }
  ];

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}


