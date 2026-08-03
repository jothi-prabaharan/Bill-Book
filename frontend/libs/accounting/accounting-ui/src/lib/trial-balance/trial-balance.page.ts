import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

interface TrialBalanceRow {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  totalDebit: number;
  totalCredit: number;
  debitBalance: number;
  creditBalance: number;
}

interface TrialBalance {
  from: string | null;
  to: string | null;
  rows: TrialBalanceRow[];
  totalDebit: number;
  totalCredit: number;
  isBalanced: boolean;
}

interface AccountType {
  accountTypeId: number;
  displayName: string;
  sortOrder: number;
}

/**
 * The trial balance. Every account with a balance, grouped by account type, and
 * the two columns at the foot.
 *
 * Those two totals agreeing is the one number that says the whole system is
 * sound, so when they do not this page says so at the top in red rather than
 * showing a tidy table with a discrepancy buried in it.
 */
@Component({
  selector: 'bb-trial-balance-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink],
  templateUrl: './trial-balance.page.html',
  styleUrl: './trial-balance.page.scss',
})
export class TrialBalancePage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly report = signal<TrialBalance | null>(null);
  protected readonly types = signal<AccountType[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  from = '';
  to = '';

  /**
   * Grouped by account type, in report order — assets, then liabilities, equity,
   * income and expense. A trial balance listed by code alone reads as a wall of
   * numbers; grouped, the balance sheet and the profit and loss are visible in it.
   */
  protected readonly grouped = computed(() => {
    const rows = this.report()?.rows ?? [];

    return this.types()
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((type) => ({
        type,
        rows: rows.filter((r) => r.accountTypeId === type.accountTypeId),
        debit: rows
          .filter((r) => r.accountTypeId === type.accountTypeId)
          .reduce((sum, r) => sum + r.debitBalance, 0),
        credit: rows
          .filter((r) => r.accountTypeId === type.accountTypeId)
          .reduce((sum, r) => sum + r.creditBalance, 0),
      }))
      .filter((group) => group.rows.length > 0);
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      const [types, report] = await Promise.all([
        this.req<AccountType[]>('GET', '/api/master/account-types'),
        this.req<TrialBalance>('GET', `/api/ledger/trial-balance${this.range()}`),
      ]);
      this.types.set(types);
      this.report.set(report);
    } catch {
      this.message.set('Could not load the trial balance.');
    } finally {
      this.busy.set(false);
    }
  }

  clear(): void {
    this.from = '';
    this.to = '';
    void this.load();
  }

  private range(): string {
    const parts: string[] = [];
    if (this.from) {
      parts.push(`from=${this.from}`);
    }
    if (this.to) {
      parts.push(`to=${this.to}`);
    }
    return parts.length === 0 ? '' : `?${parts.join('&')}`;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
