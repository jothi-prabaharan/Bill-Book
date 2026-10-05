import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  BbSelectOption,
  ColumnDef,
  DataGridComponent,
  EmailInputComponent,
  PasswordInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { BUSINESS_TYPES, Currency } from '@bill-book/auth';

interface CustomerListItem {
  customerId: string;
  customerCode: string;
  name: string;
  billingEmail: string;
  planTier: string;
  /** Standard, Internal or Demo (TK-152). */
  kind: string;
  status: string;
  createdAt: string | null;
}

/** The three kinds of customer (D-44). Internal and Demo are never billed and never lapse. */
export const CUSTOMER_KINDS: readonly BbSelectOption<string>[] = [
  { value: 'Standard', label: 'Standard' },
  { value: 'Internal', label: 'Internal' },
  { value: 'Demo', label: 'Demo' },
];

/** One demo customer after `POST api/admin/customers/demo` (TK-154). */
export interface DemoCustomerRow {
  businessType: string;
  customerCode: string;
  created: boolean;
  unseeded: string[];
}

/** What a demo run did, in one line for the page. */
export function demoSummary(rows: DemoCustomerRow[]): string {
  const created = rows.filter((r) => r.created).length;
  const unfinished = rows.filter((r) => r.unseeded.length > 0);
  const made = created === 0 ? 'All six demo customers already existed.' : `${created} demo customer${created === 1 ? '' : 's'} created.`;
  return unfinished.length === 0
    ? `${made} Every one is set up.`
    : `${made} Still to set up: ${unfinished.map((r) => `${r.customerCode} (${r.unseeded.join(', ')})`).join('; ')}. Run it again once those services are up.`;
}

/** The list's kind filter query: none for every kind. */
export function kindQuery(kind: string): string {
  return kind ? `?kind=${encodeURIComponent(kind)}` : '';
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
  kind: string;
  businessType: string;
}

/**
 * Platform admin's customer list — every customer on the one shared
 * database, not scoped to any one of them. Creating one here runs the same
 * seed as public trial signup; a customer stuck at Provisioning or Failed
 * (a partial seed — some service unreachable, most often) can be retried
 * without asking them to sign up again.
 *
 * An operator can also create an Internal or a Demo customer here, which
 * public signup cannot (TK-152): both start on Elite and never lapse.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-customers-page',
  standalone: true,
  imports: [
    DataGridComponent,
    FormsModule,
    TextInputComponent,
    SelectComponent,
    EmailInputComponent,
    PasswordInputComponent,
  ],
  templateUrl: './customers.page.html',
  styleUrl: './customers.page.scss',
})
export class CustomersPage implements OnInit {

  /** Month numbers, which is what the branch stores. */
  protected readonly monthOptions: BbSelectOption<number>[] = Array.from(
    { length: 12 },
    (_, at) => ({ value: at + 1, label: `Month ${at + 1}` }),
  );

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.currencies().map((currency) => ({
      value: currency.code,
      label: `${currency.code} - ${currency.name}`,
    })),
  );
  protected readonly kindOptions = CUSTOMER_KINDS;
  protected readonly kindFilterOptions: BbSelectOption<string>[] = [{ value: '', label: 'Every kind' }, ...CUSTOMER_KINDS];
  protected readonly businessTypeOptions: BbSelectOption<string>[] = BUSINESS_TYPES.map((t) => ({ value: t.value, label: t.label }));

  /** The list's kind filter; empty shows every kind. */
  protected readonly kindFilter = signal('');

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
    { field: 'kind', header: 'Kind' },
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
      this.rows.set(await this.get<CustomerListItem[]>(`/api/admin/customers${kindQuery(this.kindFilter())}`));
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

  protected filterByKind(kind: string | null): void {
    this.kindFilter.set(kind ?? '');
    void this.load();
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

  /**
   * Creates the six demo customers, one per business type, or finishes any a
   * previous run left unseeded (TK-154). Their owners' sign-ins are made from
   * the server's configured mailbox; each signs in through forgot-password.
   */
  async createDemos(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      const rows = await this.send<DemoCustomerRow[]>('POST', '/api/admin/customers/demo', {});
      this.message.set(demoSummary(rows));
      this.messageIsError.set(rows.some((r) => r.unseeded.length > 0));
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'Could not create the demo customers.');
    } finally {
      this.busy.set(false);
    }
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
      kind: 'Standard',
      businessType: 'Retail',
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
