import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InvoiceService, SaveInvoiceRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext, totalsOf , DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-invoice-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './invoice-form.component.html',
  styleUrl: './invoice-form.component.scss'
})
export class InvoiceFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private invoiceService = inject(InvoiceService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  invoiceId: number | null = null;
  status = 'Draft';
  saving = false;
  posting = false;
  voiding = false;

  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    dueDate: [''],
    contactId: [1, Validators.required],
    currencyCode: ['INR', Validators.required],
    exchangeRate: [1, [Validators.required, Validators.min(0.0001)]],
    billingAddress: [''],
    shippingAddress: [''],
    notes: ['']
  });
  
  lines: DocumentLine[] = [];
  context: DocumentLineContext = {
    isInterState: false,
    currencyDecimals: 2,
    allowFreeTextLines: true,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: false
  };

  get totals() {
    return totalsOf(this.lines);
  }

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit = true;
      this.invoiceId = +id;
      this.loadInvoice();
    }
  }

  loadInvoice() {
    if (!this.invoiceId) return;
    this.invoiceService.get(this.invoiceId).subscribe(inv => {
      this.status = inv.status;
      this.form.patchValue({
        documentDate: inv.documentDate,
        dueDate: inv.dueDate ?? '',
        contactId: inv.contactId,
        currencyCode: inv.currencyCode,
        exchangeRate: inv.exchangeRate,
        billingAddress: inv.billingAddress || '',
        shippingAddress: inv.shippingAddress || '',
        notes: inv.notes || ''
      });
      this.context = {
        ...this.context,
        isInterState: inv.isInterState,
        readonly: inv.status !== 'Draft'
      };
      if (inv.status !== 'Draft') {
        this.form.disable();
      }
      // Map lines from view to DocumentLine[]
      this.lines = inv.lines.map(l => ({
        detailId: l.invoiceDetailId,
        lineNumber: l.lineNumber,
        itemId: l.itemId ?? null,
        itemLabel: l.itemLabel ?? null,
        hsnSacCode: l.hsnSacCode ?? null,
        description: l.description ?? null,
        warehouseId: null,
        quantity: l.quantity,
        uomId: null, uomLabel: null,
        conversionFactor: l.conversionFactor,
        baseQuantity: l.quantity * l.conversionFactor,
        unitPrice: l.unitPrice,
        isPriceInclusive: false,
        discountPercent: l.discountPercent,
        discountAmount: l.discountAmount,
        grossAmount: l.grossAmount,
        taxableAmount: l.taxableAmount,
        taxTreatment: l.taxTreatment as any,
        taxMasterId: l.taxMasterId ?? null,
        taxGroupId: null,
        taxAmount: l.taxAmount,
        taxes: l.taxes.map(t => ({
          component: t.taxComponent as any, subAccountId: null,
          rate: t.rate,
          taxableAmount: t.taxableAmount,
          amount: t.amount
        } as any)),
        lineType: 'Stock',
        accountId: l.accountId ?? null,
        fixedAssetCategoryId: null,
        lineTotal: l.lineTotal,
        itemBatchId: null,
        lineNotes: l.lineNotes ?? null
      }));
    });
  }

  onLinesChange(newLines: readonly DocumentLine[]) {
    this.lines = [...newLines];
  }

  onPickItem(_index: number) {
    // open item picker dialog, update lines
  }

  goBack() {
    void this.router.navigate(['../'], { relativeTo: this.route });
  }

  save() {
    if (this.form.invalid) return;

    this.saving = true;
    const val = this.form.getRawValue();
    const request: SaveInvoiceRequest = {
      documentDate: val.documentDate!,
      dueDate: val.dueDate || undefined,
      contactId: val.contactId!, 
      currencyCode: val.currencyCode!,
      exchangeRate: val.exchangeRate!,
      billingAddress: val.billingAddress || undefined,
      shippingAddress: val.shippingAddress || undefined,
      notes: val.notes || undefined,
      lines: this.lines.map(l => ({
        itemId: l.itemId ?? undefined,
        description: l.description ?? undefined,
        hsnSacCode: l.hsnSacCode ?? undefined,
        accountId: l.accountId ?? undefined,
        taxTreatment: l.taxTreatment as any,
        taxMasterId: l.taxMasterId ?? undefined,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent ?? 0
      }))
    };

    if (this.isEdit && this.invoiceId) {
      this.invoiceService.update(this.invoiceId, request).subscribe({
        next: () => { void this.router.navigate(['../'], { relativeTo: this.route }); },
        error: () => { this.saving = false; }
      });
    } else {
      this.invoiceService.create(request).subscribe({
        next: () => { void this.router.navigate(['../'], { relativeTo: this.route }); },
        error: () => { this.saving = false; }
      });
    }
  }

  voidInvoice() {
    if (!this.invoiceId) return;
    const reason = prompt('Enter void reason:');
    if (!reason) return;
    this.voiding = true;
    this.invoiceService.voidInvoice(this.invoiceId, { reason }).subscribe({
      next: () => {
        this.status = 'Void';
        this.context = { ...this.context, readonly: true };
        this.form.disable();
        this.voiding = false;
      },
      error: () => { this.voiding = false; }
    });
  }

  print() {
    window.print();
  }

  postInvoice() {
    if (!this.invoiceId) return;
    this.posting = true;
    this.invoiceService.post(this.invoiceId).subscribe({
      next: () => {
        this.status = 'Posted';
        this.context = { ...this.context, readonly: true };
        this.form.disable();
        this.posting = false;
      },
      error: () => { this.posting = false; }
    });
  }
}








