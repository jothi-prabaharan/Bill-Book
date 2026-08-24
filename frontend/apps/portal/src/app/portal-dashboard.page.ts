import { ChangeDetectionStrategy, Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

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
  selector: 'bb-portal-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './portal-dashboard.page.html',
  styleUrl: './portal-dashboard.page.scss'
})
export class PortalDashboardPage implements OnInit {
  private readonly http = inject(HttpClient);

  readonly statement = signal<StatementResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal<boolean>(true);

  readonly totalBilled = computed(() => {
    const data = this.statement();
    if (!data) return 0;
    return data.transactions.reduce((sum, t) => sum + (t.debit || 0), 0);
  });

  readonly totalPaid = computed(() => {
    const data = this.statement();
    if (!data) return 0;
    return data.transactions.reduce((sum, t) => sum + (t.credit || 0), 0);
  });

  ngOnInit() {
    void this.fetchDashboardData();
  }

  async fetchDashboardData() {
    this.loading.set(true);
    this.error.set(null);
    try {
      const response = await this.http.get<StatementResponse>('/api/portal/statements').toPromise();
      if (response) {
        this.statement.set(response);
      }
    } catch (_err: any) {
      this.error.set('Could not load dashboard data.');
    } finally {
      this.loading.set(false);
    }
  }
}
