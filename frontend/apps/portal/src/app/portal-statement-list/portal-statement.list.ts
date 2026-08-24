import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-portal-statement-list',
  standalone: true,
  imports: [CommonModule, DataGridComponent, DataGridCellTemplateDirective],
  templateUrl: './portal-statement.list.html',
  styleUrl: './portal-statement.list.scss'
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
    return !!(row.transactionNo && row.transactionNo.startsWith('SIN-'));
  }

  downloadDocument(row: StatementTransaction) {
    if (!this.canDownload(row)) return;
    
    const id = row.transactionNo.split('-')[1];
    if (!id) return;

    this.http.get(`/api/sales/invoices/${id}/print`, { responseType: 'blob' }).subscribe((blob: Blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `Invoice-${id}.pdf`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    });
  }
}
