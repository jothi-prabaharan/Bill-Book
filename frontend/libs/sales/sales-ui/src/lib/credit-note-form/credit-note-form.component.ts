import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreditNoteService, SaveCreditNoteRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-credit-note-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, RouterModule],
  templateUrl: './credit-note-form.component.html',
  styleUrl: './credit-note-form.component.scss'
})
export class CreditNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private creditNoteService = inject(CreditNoteService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  creditNoteId: number | null = null;

  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    invoiceId: ['', Validators.required],
    contactId: [1, Validators.required],
    reasonCode: [1, Validators.required],
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

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit = true;
      this.creditNoteId = +id;
      this.loadCreditNote();
    }
  }

  loadCreditNote() {
    if (!this.creditNoteId) return;
    this.creditNoteService.get(this.creditNoteId).subscribe(cn => {
      this.form.patchValue({
        documentDate: cn.documentDate,
        invoiceId: cn.invoiceId.toString(),
        contactId: cn.contactId,
        reasonCode: cn.reasonCode,
        currencyCode: cn.currencyCode,
        exchangeRate: cn.exchangeRate,
        billingAddress: cn.billingAddress,
        shippingAddress: cn.shippingAddress,
        notes: cn.notes
      });
      this.lines = (cn.lines || []).map(l => ({
        itemId: l.itemId,
        description: l.description,
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
    const request: SaveCreditNoteRequest = {
      documentDate: val.documentDate!,
      invoiceId: +val.invoiceId!,
      contactId: val.contactId!,
      reasonCode: +val.reasonCode!,
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
      } as any))
    } as any;
    this.creditNoteService.save(request).subscribe(() => {
      void this.router.navigate(['../'], { relativeTo: this.route });
    });
  }
}
