import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SalesOrderService, SaveSalesOrderRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext, totalsOf, TextInputComponent } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-sales-order-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule, TextInputComponent],
  templateUrl: './sales-order-form.component.html',
  styleUrls: ['./sales-order-form.component.scss']
})
export class SalesOrderFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private salesOrderService = inject(SalesOrderService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  salesOrderId: number | null = null;
  
  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    deliveryDate: [new Date().toISOString().split('T')[0], Validators.required],
    contactId: [1, Validators.required], 
    contactGstin: [''],
    placeOfSupplyStateCode: [''],
    currencyCode: ['INR', Validators.required],
    exchangeRate: [1, [Validators.required, Validators.min(0.0001)]],
    billingAddress: [''],
    shippingAddress: [''],
    notes: [''],
    termsAndConditions: ['']
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
      this.salesOrderId = +id;
      this.loadSalesOrder();
    }
  }

  loadSalesOrder() {
    if (!this.salesOrderId) return;
    this.salesOrderService.get(this.salesOrderId).subscribe(so => {
      this.form.patchValue({
        documentDate: so.documentDate,
        deliveryDate: so.deliveryDate,
        contactId: so.contactId,
        contactGstin: so.contactGstin || '',
        placeOfSupplyStateCode: (so as any).placeOfSupplyStateCode || (so as any).placeOfSupplyStateId?.toString() || '',
        currencyCode: so.currencyCode,
        exchangeRate: so.exchangeRate,
        billingAddress: so.billingAddress || '',
        shippingAddress: so.shippingAddress || '',
        notes: so.notes,
        termsAndConditions: so.termsAndConditions
      });
      this.lines = (so.lines || []).map(l => ({
        itemId: l.itemId,
        description: l.description,
        hsnSacCode: l.hsnSacCode,
        accountId: l.accountId,
        taxTreatment: l.taxTreatment || 'Taxable',
        taxMasterId: l.taxMasterId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0
      } as any));
    });
  }

  onLinesChange(newLines: readonly DocumentLine[]) {
    this.lines = [...newLines];
  }

  onPickItem(_index: number) {
    // open item picker dialog, update lines
  }

  save() {
    if (this.form.invalid) return;

    const val = this.form.value;
    const request: SaveSalesOrderRequest = {
      documentDate: val.documentDate!,
      deliveryDate: val.deliveryDate!,
      contactId: val.contactId!,
      contactGstin: val.contactGstin || undefined,
      placeOfSupplyStateCode: val.placeOfSupplyStateCode || undefined,
      currencyCode: val.currencyCode!,
      exchangeRate: val.exchangeRate!,
      billingAddress: val.billingAddress || undefined,
      shippingAddress: val.shippingAddress || undefined,
      notes: val.notes || undefined,
      termsAndConditions: val.termsAndConditions || undefined,
      lines: this.lines.map(l => ({
        itemId: l.itemId ?? undefined,
        description: l.description ?? undefined,
        hsnSacCode: l.hsnSacCode ?? undefined,
        accountId: l.accountId ?? undefined,
        taxTreatment: l.taxTreatment,
        taxMasterId: l.taxMasterId ?? undefined,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent ?? 0
      } as any))
    };

    if (this.isEdit && this.salesOrderId) {
      this.salesOrderService.update(this.salesOrderId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.salesOrderService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
