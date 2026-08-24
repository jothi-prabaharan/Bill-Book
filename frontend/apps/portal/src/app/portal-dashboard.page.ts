import { Component, inject, OnInit, signal, computed } from '@angular/core';
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
  selector: 'bb-portal-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: \
    <main class="dashboard-container">
      <header class="dashboard-header">
        <h1>Dashboard</h1>
        <p>Overview of your account status</p>
      </header>

      <div class="summary-cards" *ngIf="statement() as data">
        <div class="bb-card summary-card">
          <h3>Opening Balance</h3>
          <p class="amount">{{ data.openingBalance | number:'1.2-2' }}</p>
        </div>
        
        <div class="bb-card summary-card">
          <h3>Total Billed</h3>
          <p class="amount text-error">{{ totalBilled() | number:'1.2-2' }}</p>
        </div>

        <div class="bb-card summary-card">
          <h3>Total Paid</h3>
          <p class="amount text-success">{{ totalPaid() | number:'1.2-2' }}</p>
        </div>

        <div class="bb-card summary-card highlight">
          <h3>Outstanding Balance</h3>
          <p class="amount">{{ data.closingBalance | number:'1.2-2' }}</p>
        </div>
      </div>

      <div *ngIf="loading()" class="loading-state">
        <p>Loading your dashboard...</p>
      </div>
      
      <div *ngIf="error()" class="error-message">
        {{ error() }}
      </div>

      <div class="actions">
        <a routerLink="/statement" class="bb-btn bb-btn-primary">View Detailed Statement</a>
      </div>
    </main>
  \,
  styles: [\
    .dashboard-container {
      padding: 24px;
      max-width: 1200px;
      margin: 0 auto;
    }

    .dashboard-header {
      margin-bottom: 32px;
      h1 { margin: 0 0 8px; color: var(--text-color); }
      p { margin: 0; color: var(--text-color-secondary); }
    }

    .summary-cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 24px;
      margin-bottom: 32px;
    }

    .summary-card {
      padding: 24px;
      background: white;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.05);

      h3 { margin: 0 0 16px; font-size: 1rem; color: var(--text-color-secondary); font-weight: 500; }
      .amount { margin: 0; font-size: 2rem; font-weight: 600; color: var(--text-color); }

      &.highlight {
        background: var(--primary-color);
        h3, .amount { color: white; }
      }
    }

    .text-error { color: var(--error-color, #dc3545) !important; }
    .text-success { color: var(--success-color, #28a745) !important; }

    .actions {
      display: flex;
      gap: 16px;
    }

    .bb-btn {
      display: inline-block;
      padding: 12px 24px;
      border-radius: 4px;
      text-decoration: none;
      font-weight: 500;
      text-align: center;
    }

    .bb-btn-primary {
      background: var(--primary-color);
      color: white;
    }

    @media (max-width: 480px) {
      .summary-cards {
        grid-template-columns: 1fr;
      }
    }
  \]
})
export class PortalDashboardPage implements OnInit {
  private readonly http = inject(HttpClient);

  readonly statement = signal<StatementResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly loading = signal<boolean>(true);

  readonly totalBilled = computed(() => {
    const data = this.statement();
    if (!data) return 0;
    // Debits are typically bills/invoices sent to the customer
    return data.transactions.reduce((sum, t) => sum + (t.debit || 0), 0);
  });

  readonly totalPaid = computed(() => {
    const data = this.statement();
    if (!data) return 0;
    // Credits are typically payments received from the customer
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
    } catch (err: any) {
      this.error.set('Could not load dashboard data.');
    } finally {
      this.loading.set(false);
    }
  }
}
