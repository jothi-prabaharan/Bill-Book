import { ChangeDetectionStrategy } from '@angular/core';
import { DataGridComponent, ColumnDef } from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface OrgCurrency {
  orgCurrencyId: string;
  currencyId: number;
  code: string;
  name: string;
  symbol: string;
  format: string;
  decimalPlaces: number;
  isBaseCurrency: boolean;
  isActive: boolean;
}

interface MasterCurrency {
  currencyId: number;
  code: string;
  name: string;
  symbol: string;
}

/**
 * Organization currencies. The list shows active currencies by default; the
 * "Show inactive" toggle reveals the rest. Add picks from the currencies not
 * yet enabled. The base currency is seeded active at org creation and cannot
 * be deactivated.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-org-currencies-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule],
  templateUrl: './org-currencies.page.html',
  styleUrl: './org-currencies.page.scss',
})
export class OrgCurrenciesPage implements OnInit {
  private readonly http = inject(HttpClient);

  /** Replace with the org id from the auth token once the org context service lands. */
  private readonly orgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  protected readonly rows = signal<OrgCurrency[]>([]);
  protected readonly available = signal<MasterCurrency[]>([]);
  protected readonly adding = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  showInactive = false;
  selectedCurrencyId = 0;

  columns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'symbol', header: 'Symbol' },
    { field: 'format', header: 'Format' },
    { field: 'decimalPlaces', header: 'Decimals' },
    { field: 'isActive', header: 'Active' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const url = `/api/organizations/${this.orgId()}/currencies?includeInactive=${this.showInactive}`;
      this.rows.set(await this.get<OrgCurrency[]>(url));
    } catch {
      this.error.set('Could not load currencies.');
    } finally {
      this.busy.set(false);
    }
  }

  async openAdd(): Promise<void> {
    this.selectedCurrencyId = 0;
    this.adding.set(true);
    this.available.set(
      await this.get<MasterCurrency[]>(`/api/organizations/${this.orgId()}/currencies/available`),
    );
  }

  async save(): Promise<void> {
    if (this.selectedCurrencyId <= 0) {
      this.error.set('Select a currency before saving.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.post(`/api/organizations/${this.orgId()}/currencies`, {
        currencyId: this.selectedCurrencyId,
      });
      this.adding.set(false);
      await this.load();
    } catch {
      this.error.set('Could not add that currency.');
    } finally {
      this.busy.set(false);
    }
  }

  async toggle(row: OrgCurrency): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.put(
        `/api/organizations/${this.orgId()}/currencies/${row.orgCurrencyId}/active`,
        { isActive: !row.isActive },
      );
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.error.set(anyErr?.error?.message ?? 'Could not change that currency.');
    } finally {
      this.busy.set(false);
    }
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private post(url: string, body: unknown): Promise<unknown> {
    return new Promise((resolve, reject) =>
      this.http.post(url, body).subscribe({ next: resolve, error: reject }),
    );
  }

  private put(url: string, body: unknown): Promise<unknown> {
    return new Promise((resolve, reject) =>
      this.http.put(url, body).subscribe({ next: resolve, error: reject }),
    );
  }
}

