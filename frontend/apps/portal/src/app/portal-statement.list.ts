import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { DataGridComponent, DataGridCellTemplateDirective, ColumnDef } from '@bill-book/ui-components';

interface StatementTransaction {
  ledgerDate: string;
  transactionNo: string;
  reference: string;
  description: string;
  debit: number;
  credit: number;
  balance: number;
}

interface StatementResponse {
  openingBalance: number;
  transactions: StatementTransaction[];
  closingBalance: number;
}

@Component({
  selector: 'bb-portal-statement-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, DataGridCellTemplateDirective],
  template: \
    <main class="statement-container">
      <header class="statement-header">
        <h1>Account Ledger</h1>
        <p>Your detailed transaction history</p>
      </header>

      <div class="bb-card">
        <div class="statement-info" *ngIf="statement() as data">
          <div class="info-row">
            <span>Opening Balance:</span>
            <strong>{{ data.openingBalance | number:'1.2-2' }}</strong>
          </div>
        </div>

        <bb-data-grid
          [data]="transactions()"
          [columns]="columns"
          gridCode="portal-statement"
          [pageSize]="25"
          [totalCount]="transactions().length"
        >
          <ng-template bbCellTemplate="actions" let-row>
            <button 
              *ngIf="canDownload(row)"
              class="bb-btn bb-btn-secondary" 
              (click)="downloadDocument(row)">
              Download
            </button>
          </ng-template>
        </bb-data-grid>

        <div class="statement-info footer-info" *ngIf="statement() as data">
          <div class="info-row">
            <span>Closing Balance:</span>
            <strong>{{ data.closingBalance | number:'1.2-2' }}</strong>
          </div>
        </div>
      </div>
    </main>
  \,
  styles: [\
    .statement-container {
      padding: 24px;
      max-width: 1200px;
      margin: 0 auto;
    }
    .statement-header {
      margin-bottom: 24px;
      h1 { margin: 0 0 8px; }
    }
    .statement-info {
      padding: 16px;
      background: var(--surface-color-variant, #f8f9fa);
      border-bottom: 1px solid var(--border-color);
      display: flex;
      justify-content: flex-end;
      font-size: 1.1rem;
    }
    .footer-info {
      border-bottom: none;
      border-top: 1px solid var(--border-color);
    }
    .info-row {
      display: flex;
      gap: 16px;
    }
    .bb-btn {
      padding: 6px 12px;
      border: 1px solid var(--border-color);
      border-radius: 4px;
      background: transparent;
      cursor: pointer;
    }
    .bb-btn:hover {
      background: var(--surface-color-variant, #f8f9fa);
    }
  \]
})
export class PortalStatementList implements OnInit {
  private readonly http = inject(HttpClient);

  readonly statement = signal<StatementResponse | null>(null);
  readonly transactions = signal<StatementTransaction[]>([]);

  readonly columns: ColumnDef[] = [
    { field: 'ledgerDate', title: 'Date', dataType: 'date' },
    { field: 'transactionNo', title: 'Transaction #', dataType: 'string' },
    { field: 'reference', title: 'Reference', dataType: 'string' },
    { field: 'debit', title: 'Debit (Billed)', dataType: 'money', align: 'right' },
    { field: 'credit', title: 'Credit (Paid)', dataType: 'money', align: 'right' },
    { field: 'balance', title: 'Running Balance', dataType: 'money', align: 'right' },
    { field: 'actions', title: 'Actions', isTemplate: true }
  ];

  ngOnInit() {
    void this.fetchData();
  }

  async fetchData() {
    try {
      const response = await this.http.get<StatementResponse>('/api/portal/statements').toPromise();
      if (response) {
        this.statement.set(response);
        this.transactions.set(response.transactions);
      }
    } catch (err) {
      console.error('Error fetching statement', err);
    }
  }

  canDownload(row: StatementTransaction): boolean {
    // Basic check if it's an invoice type transaction.
    // e.g. SIN-1234
    return row.transactionNo && row.transactionNo.startsWith('SIN-');
  }

  downloadDocument(row: StatementTransaction) {
    if (!this.canDownload(row)) return;
    
    // Extract ID (e.g. SIN-1234 -> 1234)
    const id = row.transactionNo.split('-')[1];
    if (!id) return;

    // Use window.open or hidden iframe to trigger download. We'll use window.open for simplicity.
    // The API might require auth, which window.open won't send via interceptor unless it's a cookie.
    // Alternatively, fetch the blob and trigger download.
    this.http.get(/api/sales/invoices/\/print, { responseType: 'blob' }).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = \Invoice-\.pdf\;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    });
  }
}
