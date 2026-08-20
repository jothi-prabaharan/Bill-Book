import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  QuoteService,
  SaveQuoteRequest,
  toApiLine,
  toGridLine,
} from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-quote-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule],
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
      // Scaled through the boundary rather than handed over raw. The grid works
      // in integer paise with quantity at six decimals; passing the API's rupees
      // straight in makes it compute 10 x 100 / 1,000,000 and show a total of
      // nothing, so a priced quote reads as an empty one.
      this.lines = (q.lines || []).map((l, i) => toGridLine(l, i + 1));
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
      // Back through the same boundary, and without the computed amounts: the
      // server recomputes every figure against the rates in force on the
      // quote's date, so sending the grid's would put two answers on the wire.
      lines: this.lines.map(l => toApiLine(l) as SaveQuoteRequest['lines'][number])
    };

    if (this.isEdit && this.quoteId) {
      this.quoteService.update(this.quoteId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.quoteService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
