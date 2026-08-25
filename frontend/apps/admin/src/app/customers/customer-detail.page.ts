import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ColumnDef, DataGridComponent } from '@bill-book/ui-components';

interface OrganizationRow {
  orgId: string;
  orgCode: string;
  name: string;
  vertical: string;
  baseCurrency: string;
  city: string | null;
  gstin: string | null;
  status: string;
  isFirst: boolean;
}

/**
 * One customer's branches, as the platform sees them — read-only here. A
 * branch's own details are the customer's to edit from inside their own
 * Settings › Branches, once they can sign in; this page exists so a stuck
 * customer can be diagnosed without needing their credentials.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-customer-detail-page',
  standalone: true,
  imports: [DataGridComponent, RouterLink],
  templateUrl: './customer-detail.page.html',
  styleUrl: './customer-detail.page.scss',
})
export class CustomerDetailPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  protected readonly customerId = signal('');
  protected readonly rows = signal<OrganizationRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  columns: ColumnDef[] = [
    { field: 'orgCode', header: 'Code' },
    { field: 'name', header: 'Branch' },
    { field: 'vertical', header: 'Trade' },
    { field: 'city', header: 'City' },
    { field: 'gstin', header: 'GSTIN' },
    { field: 'baseCurrency', header: 'Currency' },
    { field: 'status', header: 'Status' },
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('customerId') ?? '';
    this.customerId.set(id);
    void this.load(id);
  }

  private async load(customerId: string): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      this.rows.set(
        await this.get<OrganizationRow[]>(`/api/admin/customers/${customerId}/organizations`),
      );
    } catch {
      this.message.set('Could not load this customer’s branches.');
    } finally {
      this.busy.set(false);
    }
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }
}
