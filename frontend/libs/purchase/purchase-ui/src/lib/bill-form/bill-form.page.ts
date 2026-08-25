import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  LookupDialogComponent,
  LookupRow,
  TaxGroupOption,
  recalculate, DateInputComponent , TextInputComponent } from '@bill-book/ui-components';
import {
  BillService,
  BillView,
  GoodsReceiptService,
  PurchaseLookupService,
  SaveBillLineRequest,
  SaveBillRequest,
} from '@bill-book/purchase-core';

const QTY_SCALE = 1_000_000;
const PAISE = 100;
const RATE_SCALE = 10_000;

type Picker = 'none' | 'vendor' | 'item' | 'receipt';

/**
 * Enter a vendor bill.
 *
 * **Whether a receipt is attached changes what posting does**, and the form says
 * so rather than leaving it to be discovered: against a receipt the goods are
 * already on the shelf, so the bill clears the clearing account and moves
 * nothing; without one it does both jobs.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bill-form',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DocumentLineGridComponent,
    LookupDialogComponent,
  DateInputComponent, TextInputComponent],
  templateUrl: './bill-form.page.html',
  styleUrl: './bill-form.page.scss',
})
export class BillFormPage {
  private readonly bills = inject(BillService);
  private readonly receipts = inject(GoodsReceiptService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly billId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');
  protected readonly daysOverdue = signal(0);

  protected readonly documentDate = signal(this.today());
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected readonly contactGstin = signal('');
  protected readonly goodsReceiptId = signal<number | null>(null);
  protected readonly goodsReceiptNo = signal('');
  protected readonly vendorBillNo = signal('');
  protected readonly vendorBillDate = signal(this.today());
  protected readonly dueDate = signal('');
  protected readonly placeOfSupplyStateCode = signal('');
  protected readonly notes = signal('');
  protected readonly isInterState = signal(false);

  protected readonly lines = signal<readonly DocumentLine[]>([]);
  protected readonly taxGroups = signal<readonly TaxGroupOption[]>([]);

  /** Receipt line ids, positional with `lines`. Required on a bill against a receipt. */
  private receiptLineIds: (number | null)[] = [];

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  private pickingLine = -1;
  private searchToken = 0;

  private readonly allowFreeTextLines = signal(true);
  private readonly discountBeforeTax = signal(true);
  private readonly discountLevel = signal<'Line' | 'Header' | 'Both'>('Line');

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.billId() === null);

  protected readonly againstReceipt = computed(() => this.goodsReceiptId() !== null);

  protected readonly gridContext = computed<DocumentLineContext>(() => ({
    isInterState: this.isInterState(),
    allowFreeTextLines: this.allowFreeTextLines(),
    discountBeforeTax: this.discountBeforeTax(),
    discountLevel: this.discountLevel(),
    readonly: this.readonlyDoc() || this.againstReceipt(),
    currencyDecimals: 2,
  }));

  constructor() {
    void this.loadReferenceData();

    const id = this.route.snapshot.paramMap.get('id');
    if (id !== null && id !== 'new') {
      void this.load(Number(id));
    }
  }

  private async loadReferenceData(): Promise<void> {
    try {
      const settings = await this.lookups.settings();
      this.allowFreeTextLines.set(settings.allowFreeTextLines);
      this.discountBeforeTax.set(settings.discountBeforeTax);
      this.discountLevel.set(settings.discountLevel);
    } catch {
      // Defaults already set.
    }

    try {
      const rates = await this.lookups.purchaseTaxRates();
      this.taxGroups.set(
        rates.map((rate) => ({
          taxGroupId: rate.taxGroupId,
          taxMasterId: rate.taxMasterId,
          label: `${rate.taxName} (${rate.totalRate}%)`,
          cgstRate: rate.cgstRate,
          sgstRate: rate.sgstRate,
          igstRate: rate.igstRate,
          cessRate: rate.cessRate,
        })),
      );
    } catch {
      this.taxGroups.set([]);
    }
  }

  protected async load(id: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const view: BillView = await this.bills.get(id);

      this.billId.set(view.billId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.daysOverdue.set(view.daysOverdue);
      this.documentDate.set(view.documentDate);
      this.contactId.set(view.contactId);
      this.contactLabel.set(
        view.contactName
          ? `${view.contactCode ?? ''} ${view.contactName}`.trim()
          : String(view.contactId),
      );
      this.contactGstin.set(view.contactGstin ?? '');
      this.goodsReceiptId.set(view.goodsReceiptId ?? null);
      this.goodsReceiptNo.set(view.goodsReceiptNo ?? '');
      this.vendorBillNo.set(view.vendorBillNo);
      this.vendorBillDate.set(view.vendorBillDate);
      this.dueDate.set(view.dueDate);
      this.notes.set(view.notes ?? '');
      this.isInterState.set(view.isInterState);

      this.receiptLineIds = view.lines.map((l) => l.goodsReceiptDetailId ?? null);

      this.lines.set(
        view.lines.map((line) => ({
          detailId: line.billDetailId,
          lineNumber: line.lineNumber,
          itemId: line.itemId ?? null,
          itemLabel: line.itemLabel ?? null,
          hsnSacCode: line.hsnSacCode ?? null,
          description: line.description ?? null,
          warehouseId: line.warehouseId ?? null,
          quantity: Math.round(line.quantity * QTY_SCALE),
          uomId: line.uomId ?? null,
          uomLabel: null,
          conversionFactor: Math.round(line.conversionFactor * QTY_SCALE),
          baseQuantity: Math.round(line.baseQuantity * QTY_SCALE),
          unitPrice: Math.round(line.unitPrice * PAISE),
          isPriceInclusive: line.isPriceInclusive,
          discountPercent: line.discountPercent ?? null,
          discountAmount: Math.round(line.discountAmount * PAISE),
          grossAmount: Math.round(line.grossAmount * PAISE),
          taxableAmount: Math.round(line.taxableAmount * PAISE),
          taxTreatment: line.taxTreatment as DocumentLine['taxTreatment'],
          taxMasterId: line.taxMasterId ?? null,
          taxGroupId: line.taxGroupId ?? null,
          taxAmount: Math.round(line.taxAmount * PAISE),
          taxes: line.taxes.map((tax) => ({
            component: tax.taxComponent as 'Cgst' | 'Sgst' | 'Igst' | 'Cess',
            subAccountId: tax.subAccountId,
            rate: Math.round(tax.rate * RATE_SCALE),
            taxableAmount: Math.round(tax.taxableAmount * PAISE),
            amount: Math.round(tax.amount * PAISE),
          })),
          lineType: line.lineType as DocumentLine['lineType'],
          accountId: line.accountId ?? null,
          fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
          lineTotal: Math.round(line.lineTotal * PAISE),
          itemBatchId: line.itemBatchId ?? null,
          lineNotes: line.lineNotes ?? null,
        })),
      );
    } catch {
      this.error.set('This bill could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set(lines);
  }

  // ---- Pickers ----------------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  protected openReceiptPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('receipt');
    void this.runSearch('');
  }

  /** Detaching goes back to a bill that brings the goods in itself. */
  protected clearReceipt(): void {
    this.goodsReceiptId.set(null);
    this.goodsReceiptNo.set('');
    this.receiptLineIds = this.lines().map(() => null);
  }

  protected onPickItem(lineIndex: number): void {
    if (this.readonlyDoc() || this.againstReceipt()) {
      return;
    }

    this.pickingLine = lineIndex;
    this.picker.set('item');
    void this.runSearch('');
  }

  protected onPickerSearch(term: string): void {
    void this.runSearch(term);
  }

  private async runSearch(term: string): Promise<void> {
    const token = ++this.searchToken;
    this.pickerLoading.set(true);

    try {
      let rows: LookupRow[];

      if (this.picker() === 'vendor') {
        rows = (await this.lookups.vendors(term)).map((vendor) => ({
          id: vendor.contactId,
          code: vendor.contactCode,
          name: vendor.displayName,
          meta: vendor.gstin,
        }));
      } else if (this.picker() === 'receipt') {
        rows = (await this.lookups.billableReceipts(term, this.contactId())).map(
          (receipt) => ({
            id: receipt.goodsReceiptId,
            code: receipt.documentNo,
            name: receipt.contactName ?? '',
            meta: `${receipt.documentDate}${
              receipt.vendorDeliveryNoteNo ? ` · DN ${receipt.vendorDeliveryNoteNo}` : ''
            }`,
          }),
        );
      } else {
        rows = (await this.lookups.items(term)).map((item) => ({
          id: item.itemId,
          code: item.itemCode,
          name: item.itemName,
          meta: item.inventoryUomCode,
        }));
      }

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
    const which = this.picker();

    if (which === 'vendor') {
      this.contactId.set(row.id);
      this.contactLabel.set(`${row.code} ${row.name}`.trim());

      if (row.meta && !this.contactGstin()) {
        this.contactGstin.set(row.meta);
      }

      this.clearReceipt();
    } else if (which === 'receipt') {
      this.goodsReceiptId.set(row.id);
      this.goodsReceiptNo.set(row.code);
      void this.pullReceiptLines(row.id);
    } else {
      this.applyItem(this.pickingLine, row);
    }

    this.closePicker();
  }

  /**
   * Copies the receipt's accepted lines onto the bill.
   *
   * **Quantities and prices come from the receipt and the grid is then locked.**
   * A bill against a receipt has to agree with it to the paise, because the
   * server refuses a price that differs — purchase price variance is not
   * implemented — so letting the figures be edited here would only produce a
   * refusal at the end.
   */
  private async pullReceiptLines(receiptId: number): Promise<void> {
    try {
      const receipt = await this.receipts.get(receiptId);
      const accepted = receipt.lines.filter((line) => line.acceptedQuantity > 0);

      if (accepted.length === 0) {
        this.notice.set('Everything on that receipt was rejected, so there is nothing to bill.');
        this.clearReceipt();
        return;
      }

      this.receiptLineIds = accepted.map((line) => line.goodsReceiptDetailId);

      this.lines.set(
        accepted.map((line, index) =>
          recalculate(
            {
              detailId: 0,
              lineNumber: index + 1,
              itemId: line.itemId ?? null,
              itemLabel: line.itemLabel ?? null,
              hsnSacCode: line.hsnSacCode ?? null,
              description: line.description ?? null,
              warehouseId: line.warehouseId ?? null,
              quantity: Math.round(line.acceptedQuantity * QTY_SCALE),
              uomId: line.uomId ?? null,
              uomLabel: null,
              conversionFactor: Math.round(line.conversionFactor * QTY_SCALE),
              baseQuantity: Math.round(line.acceptedQuantity * QTY_SCALE),
              unitPrice: Math.round(line.unitPrice * PAISE),
              isPriceInclusive: line.isPriceInclusive,
              discountPercent: line.discountPercent ?? null,
              discountAmount: Math.round(line.discountAmount * PAISE),
              grossAmount: 0,
              taxableAmount: 0,
              taxTreatment: line.taxTreatment as DocumentLine['taxTreatment'],
              taxMasterId: line.taxMasterId ?? null,
              taxGroupId: line.taxGroupId ?? null,
              taxAmount: 0,
              taxes: line.taxes.map((tax) => ({
                component: tax.taxComponent as 'Cgst' | 'Sgst' | 'Igst' | 'Cess',
                subAccountId: tax.subAccountId,
                rate: Math.round(tax.rate * RATE_SCALE),
                taxableAmount: 0,
                amount: 0,
              })),
              lineType: line.lineType as DocumentLine['lineType'],
              accountId: line.accountId ?? null,
              fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
              lineTotal: 0,
              itemBatchId: line.itemBatchId ?? null,
              lineNotes: line.lineNotes ?? null,
            },
            this.gridContext(),
          ),
        ),
      );

      if (!this.contactGstin() && receipt.contactGstin) {
        this.contactGstin.set(receipt.contactGstin);
      }
    } catch {
      this.error.set('That receipt could not be read, so its lines were not copied.');
      this.clearReceipt();
    }
  }

  private applyItem(lineIndex: number, row: LookupRow): void {
    this.lines.set(
      this.lines().map((line, at) =>
        at === lineIndex
          ? recalculate(
              { ...line, itemId: row.id, itemLabel: `${row.code} - ${row.name}` },
              this.gridContext(),
            )
          : line,
      ),
    );
  }

  protected closePicker(): void {
    this.picker.set('none');
    this.pickerRows.set([]);
    this.pickingLine = -1;
  }

  protected pickerTitle(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'Choose a vendor';
      case 'receipt':
        return 'Choose a goods receipt';
      default:
        return 'Choose an item';
    }
  }

  protected pickerPlaceholder(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'Search vendors by code or name';
      case 'receipt':
        return 'Search posted receipts';
      default:
        return 'Search items by code or name';
    }
  }

  protected pickerEmptyText(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'No vendors match.';
      case 'receipt':
        return 'No posted receipts for this vendor. Only a posted receipt has put '
          + 'anything into the clearing account for a bill to clear.';
      default:
        return 'No items match.';
    }
  }

  // ---- Actions ----------------------------------------------------------

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    if (contactId === null) {
      this.error.set('Choose the vendor before saving.');
      return;
    }

    if (!this.vendorBillNo().trim()) {
      this.error.set("Enter the vendor's own bill number — the credit is claimed against it.");
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId);
      const id = this.billId();

      if (id === null) {
        const created = await this.bills.create(request);
        await this.router.navigate(['../', created.billId], { relativeTo: this.route });
      } else {
        await this.bills.update(id, request);
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
    const id = this.billId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.bills.post(id);
      await this.load(id);
      this.notice.set(
        this.againstReceipt()
          ? 'Posted. Goods Received Not Invoiced is cleared and the vendor is owed.'
          : 'Posted. Stock is in and the vendor is owed.',
      );
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  private toRequest(contactId: number): SaveBillRequest {
    const lines: SaveBillLineRequest[] = this.lines().map((line, index) => ({
      itemId: line.itemId,
      goodsReceiptDetailId: this.receiptLineIds[index] ?? null,
      description: line.description,
      hsnSacCode: line.hsnSacCode,
      warehouseId: line.warehouseId,
      quantity: line.quantity / QTY_SCALE,
      uomId: line.uomId,
      conversionFactor: line.conversionFactor / QTY_SCALE,
      unitPrice: line.unitPrice / PAISE,
      isPriceInclusive: line.isPriceInclusive,
      discountPercent: line.discountPercent,
      discountAmount: line.discountAmount / PAISE,
      taxTreatment: line.taxTreatment,
      taxGroupId: line.taxGroupId,
      lineType: line.lineType,
      accountId: line.accountId,
      fixedAssetCategoryId: line.fixedAssetCategoryId,
      itemBatchId: line.itemBatchId,
      lineNotes: line.lineNotes,
    }));

    return {
      documentDate: this.documentDate(),
      contactId,
      goodsReceiptId: this.goodsReceiptId(),
      vendorBillNo: this.vendorBillNo().trim(),
      vendorBillDate: this.vendorBillDate(),
      dueDate: this.dueDate() || null,
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      landedCostAmount: 0,
      notes: this.notes() || null,
      lines,
    };
  }

  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The bill could not be saved.';
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}

