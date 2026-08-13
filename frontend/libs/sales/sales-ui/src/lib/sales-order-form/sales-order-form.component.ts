import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SalesOrderService, SaveSalesOrderRequest } from '@bill-book/sales-core';
import { DocumentLineGridComponent, DocumentLine, DocumentLineContext } from '@bill-book/ui-components';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

@Component({
  selector: 'bb-sales-order-form',
  standalone: true,
  imports: [CommonModule, DocumentLineGridComponent, RouterModule],
  template: `
    <div class="page-container">
      <header class="page-header">
        <h1>{{ isEdit ? 'Edit SalesOrder' : 'New SalesOrder' }}</h1>
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
export class SalesOrderFormComponent implements OnInit {
  private salesOrderService = inject(SalesOrderService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  salesOrderId: number | null = null;
  
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
      this.salesOrderId = +id;
      this.loadSalesOrder();
    }
  }

  loadSalesOrder() {
    if (!this.salesOrderId) return;
    this.salesOrderService.get(this.salesOrderId).subscribe(_q => {
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
    const request: SaveSalesOrderRequest = {
      documentDate: new Date().toISOString().split('T')[0],
      deliveryDate: new Date().toISOString().split('T')[0],
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

    if (this.isEdit && this.salesOrderId) {
      this.salesOrderService.update(this.salesOrderId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.salesOrderService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}
