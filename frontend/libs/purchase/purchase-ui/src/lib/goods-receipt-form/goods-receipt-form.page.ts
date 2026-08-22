import { Component, computed, inject, signal } from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

import { ColumnDef, DataGridComponent, DateInputComponent, DocumentLine, DocumentLineContext, DocumentLineGridComponent, LookupDialogComponent, LookupRow, NumberInputComponent, TaxGroupOption, TextInputComponent, recalculate } from '@bill-book/ui-components';

import {
  GoodsReceiptService,
  GoodsReceiptView,
  PurchaseLookupService,
  PurchaseOrderService,
  SaveGoodsReceiptLineRequest,
  SaveGoodsReceiptRequest,
} from '@bill-book/purchase-core';

const QTY_SCALE = 1_000_000;
const PAISE = 100;
const RATE_SCALE = 10_000;

type Picker = 'none' | 'vendor' | 'item' | 'order';

/**
 * What arrived, line by line.
 *
 * **Kept beside the shared grid rather than inside it.** The grid edits the
 * commercial line — item, quantity, price, tax — which is identical on all nine
 * document types. Accepted against rejected, the reason, and the batch off the
 * carton are the goods receipt's own, and putting them in the shared component
 * would push a purchase concern into every sales screen.
 */
interface ReceivingLine {
  acceptedQuantity: number;
  rejectedQuantity: number;
  rejectionReason: string;
  batchNumber: string;
  batchExpiryDate: string;
  purchaseOrderDetailId: number | null;
}

/**
 * Record a delivery.
 *
 * Posting this is the first thing in the purchase module that leaves a mark
 * outside its own tables: stock goes up, the ledger takes
 * `Dr Inventory / Cr Goods Received Not Invoiced`, and the order it came against
 * moves toward closed.
 */
@Component({
  selector: 'bb-goods-receipt-form',
  standalone: true,
  imports: [DataGridComponent, 
    CommonModule,
    FormsModule,
    RouterModule,
    DocumentLineGridComponent,
    LookupDialogComponent,
  DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './goods-receipt-form.page.html',
  styleUrl: './goods-receipt-form.page.scss',
})
export class GoodsReceiptFormPage {
  private readonly receipts = inject(GoodsReceiptService);
  private readonly orders = inject(PurchaseOrderService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly goodsReceiptId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');

  protected readonly documentDate = signal(this.today());
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected readonly contactGstin = signal('');
  protected readonly purchaseOrderId = signal<number | null>(null);
  protected readonly purchaseOrderNo = signal('');
  protected readonly vendorDeliveryNoteNo = signal('');
  protected readonly vendorDeliveryNoteDate = signal('');
  protected readonly placeOfSupplyStateCode = signal('');
  protected readonly shippingAddress = signal('');
  protected readonly notes = signal('');
  protected readonly isInterState = signal(false);

  protected readonly lines = signal<readonly DocumentLine[]>([]);
  protected readonly receiving = signal<readonly ReceivingLine[]>([]);
  protected readonly taxGroups = signal<readonly TaxGroupOption[]>([]);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  private pickingLine = -1;
  private searchToken = 0;

  private readonly allowFreeTextLines = signal(false);
  private readonly discountBeforeTax = signal(true);
  private readonly discountLevel = signal<'Line' | 'Header' | 'Both'>('Line');

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.goodsReceiptId() === null);

  protected readonly gridContext = computed<DocumentLineContext>(() => ({
    isInterState: this.isInterState(),
    // A receipt receives goods, so every line names an item whatever the branch
    // allows elsewhere. Expense and capital lines belong on the bill and the
    // server refuses them here.
    allowFreeTextLines: false,
    discountBeforeTax: this.discountBeforeTax(),
    discountLevel: this.discountLevel(),
    readonly: this.readonlyDoc(),
    currencyDecimals: 2,
  }));

  /** Anything refused anywhere on the document. Drives the summary line. */
  protected readonly totalRejected = computed(() =>
    this.receiving().reduce((sum, row) => sum + (row.rejectedQuantity || 0), 0),
  );

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
      const view: GoodsReceiptView = await this.receipts.get(id);

      this.goodsReceiptId.set(view.goodsReceiptId);
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
      this.purchaseOrderId.set(view.purchaseOrderId ?? null);
      this.purchaseOrderNo.set(view.purchaseOrderNo ?? '');
      this.vendorDeliveryNoteNo.set(view.vendorDeliveryNoteNo ?? '');
      this.vendorDeliveryNoteDate.set(view.vendorDeliveryNoteDate ?? '');
      this.shippingAddress.set(view.shippingAddress ?? '');
      this.notes.set(view.notes ?? '');
      this.isInterState.set(view.isInterState);

      this.lines.set(
        view.lines.map((line) => ({
          detailId: line.goodsReceiptDetailId,
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

      this.receiving.set(
        view.lines.map((line) => ({
          acceptedQuantity: line.acceptedQuantity,
          rejectedQuantity: line.rejectedQuantity,
          rejectionReason: line.rejectionReason ?? '',
          batchNumber: '',
          batchExpiryDate: '',
          purchaseOrderDetailId: line.purchaseOrderDetailId ?? null,
        })),
      );
    } catch {
      this.error.set('This goods receipt could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Line changes come from the grid; the receiving rows have to keep step.
   *
   * A new line defaults to fully accepted, which is the ordinary delivery — the
   * storekeeper only touches these when something is wrong.
   */
  protected onLinesChange(lines: readonly DocumentLine[]): void {
    const previous = this.receiving();

    this.receiving.set(
      lines.map((line, index) => {
        const existing = previous[index];
        const delivered = line.quantity / QTY_SCALE;

        if (existing === undefined) {
          return {
            acceptedQuantity: delivered,
            rejectedQuantity: 0,
            rejectionReason: '',
            batchNumber: '',
            batchExpiryDate: '',
            purchaseOrderDetailId: null,
          };
        }

        // Changing the delivered quantity re-splits it, keeping whatever was
        // rejected — the rejection is a fact about the goods, not a proportion.
        const rejected = Math.min(existing.rejectedQuantity, delivered);

        return {
          ...existing,
          rejectedQuantity: rejected,
          acceptedQuantity: delivered - rejected,
        };
      }),
    );

    this.lines.set(lines);
  }

  protected deliveredOf(index: number): number {
    const line = this.lines()[index];
    return line === undefined ? 0 : line.quantity / QTY_SCALE;
  }

  protected itemLabelOf(index: number): string {
    const line = this.lines()[index];
    return line?.itemLabel ?? line?.description ?? `Line ${index + 1}`;
  }

  /**
   * Rejecting is what the storekeeper keys; accepting is the remainder.
   *
   * Only one of the two is editable on purpose — they have to add up to what was
   * delivered, and two boxes that must sum to a third is a form that spends its
   * life inconsistent while somebody types.
   */
  protected onRejectedChange(index: number, value: number): void {
    const delivered = this.deliveredOf(index);
    const rejected = Math.min(Math.max(value || 0, 0), delivered);

    this.receiving.set(
      this.receiving().map((row, at) =>
        at === index
          ? {
              ...row,
              rejectedQuantity: rejected,
              acceptedQuantity: delivered - rejected,
            }
          : row,
      ),
    );
  }

  protected onReceivingField(
    index: number,
    field: 'rejectionReason' | 'batchNumber' | 'batchExpiryDate',
    value: string,
  ): void {
    this.receiving.set(
      this.receiving().map((row, at) =>
        at === index ? { ...row, [field]: value } : row,
      ),
    );
  }

  // ---- Pickers ----------------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  protected openOrderPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('order');
    void this.runSearch('');
  }

  protected onPickItem(lineIndex: number): void {
    if (this.readonlyDoc()) {
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
      } else if (this.picker() === 'order') {
        rows = (await this.lookups.openOrders(term, this.contactId())).map(
          (order) => ({
            id: order.purchaseOrderId,
            code: order.documentNo,
            name: order.contactName ?? '',
            meta: `${order.documentDate} · ${order.fulfilmentStatus}`,
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

      // The order belonged to the previous vendor; keeping it would file the
      // receipt against somebody else's order, which the server refuses anyway.
      this.purchaseOrderId.set(null);
      this.purchaseOrderNo.set('');
    } else if (which === 'order') {
      this.purchaseOrderId.set(row.id);
      this.purchaseOrderNo.set(row.code);
      void this.pullOrderLines(row.id);
    } else {
      this.applyItem(this.pickingLine, row);
    }

    this.closePicker();
  }

  /**
   * Copies the order's outstanding lines onto the receipt.
   *
   * The delivered quantity defaults to what is still owed, not to the order's
   * full quantity — a second delivery against a partly received order should
   * open showing what is left, which is what the storekeeper is holding.
   */
  private async pullOrderLines(orderId: number): Promise<void> {
    try {
      const order = await this.orders.get(orderId);

      const outstanding = order.lines.filter(
        (line) => line.quantity - line.receivedQuantity > 0,
      );

      if (outstanding.length === 0) {
        this.notice.set('That order has been received in full.');
        return;
      }

      this.lines.set(
        outstanding.map((line, index) => ({
          detailId: 0,
          lineNumber: index + 1,
          itemId: line.itemId ?? null,
          itemLabel: line.itemLabel ?? null,
          hsnSacCode: line.hsnSacCode ?? null,
          description: line.description ?? null,
          warehouseId: line.warehouseId ?? null,
          quantity: Math.round((line.quantity - line.receivedQuantity) * QTY_SCALE),
          uomId: line.uomId ?? null,
          uomLabel: null,
          conversionFactor: Math.round(line.conversionFactor * QTY_SCALE),
          baseQuantity: Math.round((line.quantity - line.receivedQuantity) * QTY_SCALE),
          unitPrice: Math.round(line.unitPrice * PAISE),
          isPriceInclusive: line.isPriceInclusive,
          discountPercent: line.discountPercent ?? null,
          discountAmount: 0,
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
          lineType: 'Stock' as const,
          accountId: null,
          fixedAssetCategoryId: null,
          lineTotal: 0,
          itemBatchId: null,
          lineNotes: null,
        })).map((line) => recalculate(line, this.gridContext())),
      );

      this.receiving.set(
        outstanding.map((line) => ({
          acceptedQuantity: line.quantity - line.receivedQuantity,
          rejectedQuantity: 0,
          rejectionReason: '',
          batchNumber: '',
          batchExpiryDate: '',
          purchaseOrderDetailId: line.purchaseOrderDetailId,
        })),
      );

      if (!this.contactGstin() && order.contactGstin) {
        this.contactGstin.set(order.contactGstin);
      }
    } catch {
      this.error.set('That order could not be read, so its lines were not copied.');
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
      case 'order':
        return 'Choose a purchase order';
      default:
        return 'Choose an item';
    }
  }

  protected pickerPlaceholder(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'Search vendors by code or name';
      case 'order':
        return 'Search issued orders';
      default:
        return 'Search items by code or name';
    }
  }

  protected pickerEmptyText(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'No vendors match. A contact has to be marked as a vendor to appear here.';
      case 'order':
        return 'No open orders for this vendor. Only issued orders that are not yet '
          + 'fully received can be received against.';
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

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId);
      const id = this.goodsReceiptId();

      if (id === null) {
        const created = await this.receipts.create(request);
        await this.router.navigate(['../', created.goodsReceiptId], {
          relativeTo: this.route,
        });
      } else {
        await this.receipts.update(id, request);
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
    const id = this.goodsReceiptId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.receipts.post(id);
      await this.load(id);
      this.notice.set('Received. Stock is on the shelf and the ledger has it.');
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  private toRequest(contactId: number): SaveGoodsReceiptRequest {
    const receiving = this.receiving();

    const lines: SaveGoodsReceiptLineRequest[] = this.lines().map((line, index) => {
      const row = receiving[index];

      return {
        itemId: line.itemId,
        purchaseOrderDetailId: row?.purchaseOrderDetailId ?? null,
        description: line.description,
        hsnSacCode: line.hsnSacCode,
        warehouseId: line.warehouseId,
        quantity: line.quantity / QTY_SCALE,
        acceptedQuantity: row?.acceptedQuantity ?? line.quantity / QTY_SCALE,
        rejectedQuantity: row?.rejectedQuantity ?? 0,
        rejectionReason: row?.rejectionReason || null,
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
        batchNumber: row?.batchNumber || null,
        batchExpiryDate: row?.batchExpiryDate || null,
        lineNotes: line.lineNotes,
      };
    });

    return {
      documentDate: this.documentDate(),
      contactId,
      purchaseOrderId: this.purchaseOrderId(),
      vendorDeliveryNoteNo: this.vendorDeliveryNoteNo() || null,
      vendorDeliveryNoteDate: this.vendorDeliveryNoteDate() || null,
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      shippingAddress: this.shippingAddress() || null,
      notes: this.notes() || null,
      lines,
    };
  }

  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The goods receipt could not be saved.';
  }

  columns: ColumnDef[] = [
    { field: 'item', header: 'Item' },
    { field: 'delivered', header: 'Delivered' },
    { field: 'rejected', header: 'Rejected' },
    { field: 'accepted', header: 'Accepted' },
    { field: 'reason', header: 'Reason' },
    { field: 'batch', header: 'Batch' },
    { field: 'expiry', header: 'Expiry' }
  ];

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}


