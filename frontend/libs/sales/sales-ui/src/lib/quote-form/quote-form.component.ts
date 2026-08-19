import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuoteService, SaveQuoteRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext , TextInputComponent } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-quote-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule, TextInputComponent],
  templateUrl: './quote-form.component.html',
  styleUrls: ['./quote-form.component.scss']
})
export class QuoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private quoteService = inject(QuoteService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  quoteId: number | null = null;

  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    validUntil: [new Date().toISOString().split('T')[0], Validators.required],
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

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit = true;
      this.quoteId = +id;
      this.loadQuote();
    }
  }

  loadQuote() {
    if (!this.quoteId) return;
    this.quoteService.get(this.quoteId).subscribe(q => {
      this.form.patchValue({
        documentDate: q.documentDate,
        validUntil: q.validUntil,
        contactId: q.contactId,
        contactGstin: q.contactGstin || '',
        placeOfSupplyStateCode: (q as any).placeOfSupplyStateCode || (q as any).placeOfSupplyStateId?.toString() || '',
        currencyCode: q.currencyCode,
        exchangeRate: q.exchangeRate,
        billingAddress: q.billingAddress || '',
        shippingAddress: q.shippingAddress || '',
        notes: q.notes,
        termsAndConditions: q.termsAndConditions
      });
      // Map lines if API returned them
      this.lines = (q.lines || []).map(l => ({
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
    const request: SaveQuoteRequest = {
      documentDate: val.documentDate!,
      validUntil: val.validUntil!,
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
      } as any) )
    };

    if (this.isEdit && this.quoteId) {
      this.quoteService.update(this.quoteId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.quoteService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
