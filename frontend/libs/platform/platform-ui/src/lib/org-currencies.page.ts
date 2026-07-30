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
  selector: 'bb-org-currencies-page',
  standalone: true,
  imports: [FormsModule],
  template: `
    <header class="page-head">
      <h1>Currencies</h1>
      <div class="head-actions">
        <label class="inline">
          <input type="checkbox" [(ngModel)]="showInactive" (ngModelChange)="load()" />
          Show inactive
        </label>
        <button type="button" (click)="openAdd()" [disabled]="busy()">+ Add currency</button>
      </div>
    </header>

    @if (adding()) {
      <div class="add-row">
        <select [(ngModel)]="selectedCurrencyId">
          <option [ngValue]="0">Select a currency…</option>
          @for (c of available(); track c.currencyId) {
            <option [ngValue]="c.currencyId">{{ c.code }} — {{ c.name }} ({{ c.symbol }})</option>
          }
        </select>
        <button type="button" (click)="save()" [disabled]="!selectedCurrencyId || busy()">Save</button>
        <button type="button" class="link" (click)="adding.set(false)">Cancel</button>
      </div>
    }

    @if (error()) {
      <p class="error">{{ error() }}</p>
    }

    <table class="grid">
      <thead>
        <tr>
          <th>Code</th>
          <th>Name</th>
          <th>Symbol</th>
          <th>Format</th>
          <th>Decimals</th>
          <th>Active</th>
        </tr>
      </thead>
      <tbody>
        @for (row of rows(); track row.orgCurrencyId) {
          <tr [class.inactive]="!row.isActive">
            <td>
              {{ row.code }}
              @if (row.isBaseCurrency) {
                <span class="badge">Base</span>
              }
            </td>
            <td>{{ row.name }}</td>
            <td>{{ row.symbol }}</td>
            <td><code>{{ row.format }}</code></td>
            <td>{{ row.decimalPlaces }}</td>
            <td>
              <button
                type="button"
                class="toggle"
                [class.on]="row.isActive"
                [disabled]="row.isBaseCurrency || busy()"
                [title]="row.isBaseCurrency ? 'The base currency cannot be deactivated' : 'Toggle active'"
                (click)="toggle(row)"
              >
                {{ row.isActive ? 'Active' : 'Inactive' }}
              </button>
            </td>
          </tr>
        } @empty {
          <tr><td colspan="6">No currencies.</td></tr>
        }
      </tbody>
    </table>
  `,
  styles: `
    .page-head { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap; }
    .head-actions { display: flex; align-items: center; gap: 1rem; }
    .inline { display: inline-flex; align-items: center; gap: .35rem; font-size: .85rem; }
    .add-row { display: flex; gap: .5rem; align-items: center; margin: 1rem 0; flex-wrap: wrap; }
    .grid { width: 100%; border-collapse: collapse; margin-top: 1rem; }
    .grid th, .grid td { text-align: left; padding: .55rem .6rem; border-bottom: 1px solid #e2e4ea; }
    .grid tr.inactive { opacity: .55; }
    .badge { font-size: .65rem; background: #3557d6; color: #fff; padding: .1rem .35rem; border-radius: 4px; margin-left: .35rem; }
    .toggle { border: 1px solid #cdd1dc; border-radius: 999px; padding: .2rem .7rem; background: #eee; cursor: pointer; font: inherit; font-size: .8rem; }
    .toggle.on { background: #d8f5e3; border-color: #55b880; }
    .toggle:disabled { cursor: not-allowed; }
    .error { color: #c0392b; }
    .link { background: none; border: 0; color: #3557d6; cursor: pointer; }

    @media (max-width: 600px) {
      .grid thead { display: none; }
      .grid tr { display: grid; gap: .2rem; padding: .6rem 0; border-bottom: 1px solid #e2e4ea; }
      .grid td { border: 0; padding: .1rem 0; }
    }
  `,
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
