import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  recalculate,
} from '@bill-book/ui-components';
import {
  PurchaseOrderService,
  PurchaseOrderView,
  SavePurchaseOrderLineRequest,
  SavePurchaseOrderRequest,
} from '@bill-book/purchase-core';

/**
 * Quantity carries six decimals in the grid, prices and amounts whole paise.
 * The API speaks decimals, so the two scales are crossed exactly here and
 * nowhere else.
 */
const QTY_SCALE = 1_000_000;
const PAISE = 100;

/**
 * Raise or amend a purchase order.
 *
 * **There is no reservation control on this form, and that is the point.** A
 * sales order form has one because confirming it holds stock back; ordering
 * from a vendor holds nothing, because the goods are not here. What this page
 * adds beside the shared line grid is the expected delivery date — see
 * `docs/modules/Purchase.md` §9a.
 */
@Component({
  selector: 'bb-purchase-order-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, DocumentLineGridComponent],
  templateUrl: './purchase-order-form.page.html',
  styleUrl: './purchase-order-form.page.scss',
})
export class PurchaseOrderFormPage {
  private readonly orders = inject(PurchaseOrderService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly purchaseOrderId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');
  protected readonly fulfilmentStatus = signal('Open');
  protected readonly receiptCount = signal(0);

  protected readonly documentDate = signal(this.today());
  protected readonly expectedDate = signal<string>('');
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactGstin = signal('');
  protected readonly placeOfSupplyStateCode = signal('');
  protected readonly billingAddress = signal('');
  protected readonly shippingAddress = signal('');
  protected readonly notes = signal('');
  protected readonly termsAndConditions = signal('');
  protected readonly isInterState = signal(false);

  protected readonly lines = signal<readonly DocumentLine[]>([]);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Posted and void orders are read-only; a draft and a ready-to-post one are not. */
  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.purchaseOrderId() === null);

  protected readonly gridContext = computed<DocumentLineContext>(() => ({
    isInterState: this.isInterState(),
    // Branch settings arrive with T4.5's header work; these are the shipped
    // defaults until then, and they only affect what the grid lets you key.
    allowFreeTextLines: true,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: this.readonlyDoc(),
    currencyDecimals: 2,
  }));

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
      const view: PurchaseOrderView = await this.orders.get(id);

      this.purchaseOrderId.set(view.purchaseOrderId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.fulfilmentStatus.set(view.fulfilmentStatus);
      this.receiptCount.set(view.receiptCount);
      this.documentDate.set(view.documentDate);
      this.expectedDate.set(view.expectedDate ?? '');
      this.contactId.set(view.contactId);
      this.contactGstin.set(view.contactGstin ?? '');
      this.billingAddress.set(view.billingAddress ?? '');
      this.shippingAddress.set(view.shippingAddress ?? '');
      this.notes.set(view.notes ?? '');
      this.termsAndConditions.set(view.termsAndConditions ?? '');
      this.isInterState.set(view.isInterState);

      this.lines.set(
        view.lines.map((line) => ({
          detailId: line.purchaseOrderDetailId,
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
            rate: Math.round(tax.rate * 10_000),
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
      this.error.set('This purchase order could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set(lines);
  }

  protected addLine(): void {
    const next: DocumentLine = recalculate(
      {
        detailId: 0,
        lineNumber: this.lines().length + 1,
        itemId: null,
        itemLabel: null,
        hsnSacCode: null,
        description: null,
        warehouseId: null,
        quantity: QTY_SCALE,
        uomId: null,
        uomLabel: null,
        conversionFactor: QTY_SCALE,
        baseQuantity: QTY_SCALE,
        unitPrice: 0,
        isPriceInclusive: false,
        discountPercent: null,
        discountAmount: 0,
        grossAmount: 0,
        taxableAmount: 0,
        taxTreatment: 'Taxable',
        taxMasterId: null,
        taxGroupId: null,
        taxAmount: 0,
        taxes: [],
        lineType: 'Stock',
        accountId: null,
        fixedAssetCategoryId: null,
        lineTotal: 0,
        itemBatchId: null,
        lineNotes: null,
      },
      this.gridContext(),
    );

    this.lines.set([...this.lines(), next]);
  }

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    if (contactId === null) {
      this.error.set('Choose the vendor before saving.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      const request = this.toRequest(contactId);
      const id = this.purchaseOrderId();

      if (id === null) {
        const created = await this.orders.create(request);
        await this.router.navigate(['../', created.purchaseOrderId], {
          relativeTo: this.route,
        });
      } else {
        await this.orders.update(id, request);
        await this.load(id);
      }
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async confirm(): Promise<void> {
    const id = this.purchaseOrderId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);

    try {
      await this.orders.confirm(id);
      await this.load(id);
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Money and quantities go back to the server as decimals. The grid holds
   * paise and six-decimal quantities, so this is the one place the scales are
   * crossed — and totals are not sent at all, because the server computes them.
   */
  private toRequest(contactId: number): SavePurchaseOrderRequest {
    const lines: SavePurchaseOrderLineRequest[] = this.lines().map((line) => ({
      itemId: line.itemId,
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
      expectedDate: this.expectedDate() || null,
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      billingAddress: this.billingAddress() || null,
      shippingAddress: this.shippingAddress() || null,
      notes: this.notes() || null,
      termsAndConditions: this.termsAndConditions() || null,
      lines,
    };
  }

  /**
   * The server's own words when it sent any — every refusal it returns is
   * something the person at the screen can act on, so replacing them with a
   * generic message throws away the only useful part.
   */
  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The purchase order could not be saved.';
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
