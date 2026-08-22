import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DeliveryChallanService, SaveDeliveryChallanRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext, totalsOf, DateInputComponent, TextInputComponent, NumberInputComponent } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-delivery-challan-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './delivery-challan-form.component.html',
  styleUrls: ['./delivery-challan-form.component.scss']
})
export class DeliveryChallanFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private deliveryChallanService = inject(DeliveryChallanService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  challanId: number | null = null;
  
  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    contactId: [1, Validators.required], 
    challanType: [0, Validators.required],
    vehicleNo: [''],
    dispatchDate: [new Date().toISOString().split('T')[0], Validators.required],
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
      this.challanId = +id;
      this.loadChallan();
    }
  }

  loadChallan() {
    if (!this.challanId) return;
    this.deliveryChallanService.get(this.challanId).subscribe(ch => {
      this.form.patchValue({
        documentDate: ch.documentDate,
        contactId: ch.contactId,
        challanType: ch.challanType,
        vehicleNo: ch.vehicleNo,
        dispatchDate: ch.dispatchDate,
        currencyCode: ch.currencyCode,
        exchangeRate: ch.exchangeRate,
        billingAddress: ch.billingAddress,
        shippingAddress: ch.shippingAddress,
        notes: ch.notes
      });
      this.lines = (ch.lines || []).map(l => ({
        itemId: l.itemId,
        description: l.description,
        hsnSacCode: l.hsnSacCode,
        accountId: l.accountId,
        taxTreatment: l.taxTreatment || 'Taxable',
        taxMasterId: l.taxMasterId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0
      })) as any;
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
    const request: SaveDeliveryChallanRequest = {
      documentDate: val.documentDate!,
      contactId: val.contactId!,
      challanType: +val.challanType!,
      vehicleNo: val.vehicleNo || undefined,
      dispatchDate: val.dispatchDate!,
      currencyCode: val.currencyCode!,
      exchangeRate: val.exchangeRate!,
      billingAddress: val.billingAddress || undefined,
      shippingAddress: val.shippingAddress || undefined,
      notes: val.notes || undefined,
      lines: this.lines.map(l => ({
        itemId: l.itemId ?? 0,
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

    if (this.isEdit && this.challanId) {
      this.deliveryChallanService.update(this.challanId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.deliveryChallanService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
