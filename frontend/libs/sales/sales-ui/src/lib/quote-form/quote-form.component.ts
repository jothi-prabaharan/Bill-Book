import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QuoteService, SaveQuoteRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-quote-form',
  standalone: true,
  imports: [CommonModule, DocumentLineGridComponent, RouterModule],
  template: `
    <div class="page-container">
      <header class="page-header">
        <h1>{{ isEdit ? 'Edit Quote' : 'New Quote' }}</h1>
        <button class="primary" (click)="save()">Save</button>
      </header>

      <!-- Minimal form scaffold, skipping reactive forms for brevity -->
      <div class="form-grid">
        <!-- Date, Contact, Valid Until, etc would go here -->
      </div>

      <div class="line-grid-container">
        <bb-document-line-grid
          [lines]="lines"
          [context]="context"
          (linesChange)="onLinesChange($event)"
          (pickItem)="onPickItem($event)"
        ></bb-document-line-grid>
      </div>
    </div>
  `,
  styles: [`
    .page-container { padding: 24px; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .line-grid-container { margin-top: 24px; border: 1px solid #eee; border-radius: 8px; }
  `]
})
export class QuoteFormComponent implements OnInit {
  private quoteService = inject(QuoteService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  quoteId: number | null = null;
  
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
    this.quoteService.get(this.quoteId).subscribe(_q => {
      // mapping logic...
    });
  }

  onLinesChange(newLines: readonly DocumentLine[]) {
    this.lines = [...newLines];
  }

  onPickItem(_index: number) {
    // open item picker dialog, update lines
  }

  save() {
    const request: SaveQuoteRequest = {
      documentDate: new Date().toISOString().split('T')[0],
      validUntil: new Date().toISOString().split('T')[0],
      contactId: 1, // mock
      placeOfSupplyStateId: 1, // mock
      currencyCode: 'INR',
      exchangeRate: 1,
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
      }))
    };

    if (this.isEdit && this.quoteId) {
      this.quoteService.update(this.quoteId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.quoteService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
