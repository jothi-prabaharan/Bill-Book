import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ColumnDef, DataGridComponent, TextInputComponent } from '@bill-book/ui-components';
import { Currency } from '@bill-book/auth';

interface CustomerListItem {
  customerId: string;
  customerCode: string;
  name: string;
  billingEmail: string;
  planTier: string;
  status: string;
  createdAt: string | null;
}

interface CreateCustomerForm {
  displayName: string;
  email: string;
  password: string;
  mobileNumber: string | null;
  companyName: string;
  organizationName: string;
  financialYearStartMonth: number;
  baseCurrency: string;
}

/**
 * Platform admin's customer list — every customer on the one shared
 * database, not scoped to any one of them. Creating one here runs the same
 * seed as public trial signup; a customer stuck at Provisioning or Failed
 * (a partial seed — some service unreachable, most often) can be retried
 * without asking them to sign up again.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-customers-page',
  standalone: true,
  imports: [DataGridComponent, FormsModule, TextInputComponent],
  templateUrl: './customers.page.html',
  styleUrl: './customers.page.scss',
})
export class CustomersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** India. The same default the branch-creation form uses. */
  private readonly defaultCountryId = 1;

  protected readonly rows = signal<CustomerListItem[]>([]);
  protected readonly currencies = signal<Currency[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly adding = signal(false);

  form: CreateCustomerForm = this.blank();

  columns: ColumnDef[] = [
    { field: 'customerCode', header: 'Code' },
    { field: 'name', header: 'Customer' },
    { field: 'billingEmail', header: 'Billing email' },
    { field: 'planTier', header: 'Plan' },
    { field: 'status', header: 'Status' },
    { field: 'createdAt', header: 'Created' },
    { field: 'actions', header: 'Actions' },
  ];

  ngOnInit(): void {
    void this.load();
    void this.loadCurrencies();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<CustomerListItem[]>('/api/admin/customers'));
    } catch {
      this.fail('Could not load customers.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadCurrencies(): Promise<void> {
    try {
      this.currencies.set(await this.get<Currency[]>('/api/master/currencies'));
    } catch {
      // The signup form defaults to INR regardless.
    }
  }

  open(row: CustomerListItem): void {
    void this.router.navigate(['/customers', row.customerId]);
  }

  protected dateOnly(value: string | null): string {
    return value ? value.slice(0, 10) : '—';
  }

  startAdd(): void {
    this.adding.set(true);
    this.form = this.blank();
  }

  async create(): Promise<void> {
    if (
      !this.hasText(this.form.displayName) ||
      !this.hasText(this.form.email) ||
      !this.hasText(this.form.password) ||
      !this.hasText(this.form.companyName) ||
      !this.hasText(this.form.organizationName)
    ) {
      this.fail('Name, email, password, company and organization name are all required.');
      return;
    }

    const body = {
      ...this.form,
      displayName: this.form.displayName.trim(),
      email: this.form.email.trim(),
      companyName: this.form.companyName.trim(),
      organizationName: this.form.organizationName.trim(),
      baseCurrency: this.form.baseCurrency.trim().toUpperCase(),
      countryId: this.defaultCountryId,
    };

    await this.run(async () => {
      await this.send('POST', '/api/admin/customers', body);
      this.adding.set(false);
    }, 'Customer created and its books set up.');
  }

  /** Re-runs the seed for a customer that never finished provisioning. */
  async retry(row: CustomerListItem): Promise<void> {
    await this.run(
      () => this.send('POST', `/api/admin/customers/${row.customerId}/retry-provisioning`, {}),
      'Retried — see status below.',
    );
  }

  private blank(): CreateCustomerForm {
    return {
      displayName: '',
      email: '',
      password: '',
      mobileNumber: null,
      companyName: '',
      organizationName: '',
      financialYearStartMonth: 4,
      baseCurrency: 'INR',
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
