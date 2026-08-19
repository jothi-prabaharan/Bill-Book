import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreditNoteService, LedgerService, OutstandingBalance, SaveCreditNoteRequest } from '@bill-book/sales-core';
import { AllocationGridComponent, AllocationRow, DocumentLineGridComponent, DocumentLine, DocumentLineContext, totalsOf, DateInputComponent, TextInputComponent, NumberInputComponent } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

/**
 * The grid holds paise and six-decimal quantities; the API speaks decimals.
 * The two scales are crossed exactly here and nowhere else — the server
 * recomputes every figure from what is sent, totals included.
 */
const QTY_SCALE = 1_000_000;
const PAISE = 100;

@Component({
  selector: 'bb-credit-note-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DocumentLineGridComponent, AllocationGridComponent, RouterModule, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './credit-note-form.component.html',
  styleUrl: './credit-note-form.component.scss'
})
export class CreditNoteFormComponent implements OnInit {
  private fb = inject(FormBuilder);
  private creditNoteService = inject(CreditNoteService);
  private ledgerService = inject(LedgerService);
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

  allocationRows: AllocationRow[] = [];
  allocationMessage = '';

  /**
   * What the note will claim, in rupees — the same unit the ledger and the
   * outstanding balances speak. The server recomputes the total on save; this
   * is the grid's best preview of it.
   */
  get amountToAllocate(): number {
    return this.totals.totalAmount / PAISE;
  }

  get totals() {
    return totalsOf(this.lines);
  }

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
        detailId: l.creditNoteDetailId,
        lineNumber: l.creditNoteDetailId,
        itemId: l.itemId,
        description: l.description,
        quantity: Math.round(l.quantity * QTY_SCALE),
        unitPrice: Math.round(l.unitPrice * PAISE),
        discountPercent: l.discountPercent || 0
      } as any));
      this.loadOutstanding();
    });
  }

  /**
   * The invoices the note can settle: the contact's outstanding CONTROL
   * balances, kept to invoices with something actually left to claim. A
   * payment's negative balance is not an invoice and an invoice that is fully
   * settled owes nothing.
   */
  loadOutstanding() {
    const contactId = this.form.get('contactId')?.value;
    if (!contactId || contactId <= 0) {
      this.allocationRows = [];
      return;
    }

    this.ledgerService.outstandingBalances(contactId).subscribe(balances => {
      this.allocationRows = balances
        .filter(b => b.transactionTypeCode === 'INV' && b.outstandingAmount > 0)
        .map(b => this.toAllocationRow(b));
    });
  }

  private toAllocationRow(b: OutstandingBalance): AllocationRow {
    return {
      transactionId: b.transactionId,
      documentNo: b.documentNo,
      documentDate: b.documentDate,
      dueDate: b.dueDate,
      totalAmount: b.totalAmount,
      outstandingAmount: b.outstandingAmount,
      allocatedAmount: 0
    };
  }

  onContactChange() {
    this.form.get('invoiceId')?.setValue('');
    this.allocationMessage = '';
    this.loadOutstanding();
  }

  /**
   * The grid is the invoice picker. A credit note names exactly one invoice —
   * GST requires it — so the allocated rows decide the invoice, and two of
   * them is a refusal, shown here rather than discovered at the ledger.
   */
  onAllocationRowsChange(rows: AllocationRow[]) {
    this.allocationRows = rows;

    const allocated = rows.filter(r => (r.allocatedAmount || 0) > 0);

    if (allocated.length === 1) {
      this.form.get('invoiceId')?.setValue(allocated[0].transactionId.toString());
      this.allocationMessage = '';
    } else if (allocated.length > 1) {
      this.form.get('invoiceId')?.setValue('');
      this.allocationMessage =
        'A credit note corrects exactly one invoice. Reduce the allocation to a single invoice.';
    } else {
      this.form.get('invoiceId')?.setValue('');
      this.allocationMessage = '';
    }
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
        invoiceDetailId: l.detailId ?? undefined,
        itemId: l.itemId ?? undefined,
        description: l.description ?? undefined,
        quantity: l.quantity / QTY_SCALE,
        unitPrice: l.unitPrice / PAISE,
        discountPercent: l.discountPercent ?? 0,
        taxGroupIds: []
      } as any))
    } as any;
    this.creditNoteService.save(request).subscribe(() => {
      void this.router.navigate(['../'], { relativeTo: this.route });
    });
  }
}