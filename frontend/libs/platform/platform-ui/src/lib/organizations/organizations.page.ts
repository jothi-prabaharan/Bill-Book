import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@bill-book/auth';

interface Organization {
  orgId: string;
  orgCode: string;
  name: string;
  baseCurrency: string;
  financialYearStartMonth: number;
  gstin: string | null;
  pan: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateId: number | null;
  postalCode: string | null;
  countryId: number;
  phoneNumber: string | null;
  mobileNumber: string | null;
  email: string | null;
  website: string | null;
  status: string;
  isFirst: boolean;
  isTrial: boolean;
  expiryDate: string | null;
}

interface State {
  stateId: number;
  stateCode: string;
  stateName: string;
}

type OrganizationForm = Omit<
  Organization,
  'orgId' | 'status' | 'isFirst' | 'isTrial' | 'expiryDate'
>;

/**
 * Settings › Branches. Each branch is a complete set of books — its own items,
 * contacts, stock, chart of accounts and numbering — sharing the head office's
 * database and separated by its organization id.
 *
 * Creating one is a small provisioning, not an insert: the books have to be
 * written before the branch is usable, so a new branch stays "setting up" until
 * every service has seeded it.
 */
@Component({
  selector: 'bb-organizations-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './organizations.page.html',
  styleUrl: './organizations.page.scss',
})
export class OrganizationsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  /** India. Replace with the head office's country once that is on the token. */
  private readonly defaultCountryId = 1;

  protected readonly rows = signal<Organization[]>([]);
  protected readonly states = signal<State[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<string | null>(null);

  protected readonly currentOrgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  form: OrganizationForm = this.blank();

  ngOnInit(): void {
    void this.load();
    void this.loadStates();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<Organization[]>('/api/organizations'));
    } catch {
      this.fail('Could not load branches.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadStates(): Promise<void> {
    try {
      this.states.set(
        await this.get<State[]>(`/api/master/countries/${this.defaultCountryId}/states`),
      );
    } catch {
      // The GSTIN-versus-state check runs server-side regardless.
    }
  }

  startAdd(): void {
    this.editingId.set('');
    this.form = this.blank();
  }

  startEdit(row: Organization): void {
    this.editingId.set(row.orgId);
    this.form = {
      orgCode: row.orgCode,
      name: row.name,
      baseCurrency: row.baseCurrency,
      financialYearStartMonth: row.financialYearStartMonth,
      gstin: row.gstin,
      pan: row.pan,
      addressLine1: row.addressLine1,
      addressLine2: row.addressLine2,
      city: row.city,
      stateId: row.stateId,
      postalCode: row.postalCode,
      countryId: row.countryId,
      phoneNumber: row.phoneNumber,
      mobileNumber: row.mobileNumber,
      email: row.email,
      website: row.website,
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();

    await this.run(async () => {
      if (id === '') {
        await this.send('POST', '/api/organizations', this.form);
      } else {
        await this.send('PUT', `/api/organizations/${id}`, this.form);
      }
      this.editingId.set(null);
    }, id === '' ? 'Branch created and its books set up.' : 'Branch saved.');
  }

  /** Re-runs the seeding for a branch that was created but never finished. */
  async retrySeeding(row: Organization): Promise<void> {
    await this.run(
      () => this.send('POST', `/api/organizations/${row.orgId}/retry-seeding`, {}),
      'Branch set up.',
    );
  }

  async suspend(row: Organization): Promise<void> {
    await this.run(
      () => this.send('DELETE', `/api/organizations/${row.orgId}`, {}),
      'Branch suspended.',
    );
  }

  /**
   * Moves into another branch. The whole app follows, because a branch is a
   * separate set of books — so the page is reloaded rather than left showing
   * data from the branch just left.
   */
  async switchTo(row: Organization): Promise<void> {
    this.busy.set(true);
    try {
      await this.auth.switchOrganization(row.orgId);
      window.location.reload();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'Could not switch to that branch.');
      this.busy.set(false);
    }
  }

  /** The base currency is fixed once the branch exists — every posting converted to it. */
  protected get currencyLocked(): boolean {
    const id = this.editingId();
    return id !== null && id !== '';
  }

  protected stateNameOf(stateId: number | null): string {
    if (stateId === null) {
      return '—';
    }

    return this.states().find((s) => s.stateId === stateId)?.stateName ?? String(stateId);
  }

  private blank(): OrganizationForm {
    return {
      orgCode: '',
      name: '',
      baseCurrency: 'INR',
      financialYearStartMonth: 4,
      gstin: null,
      pan: null,
      addressLine1: null,
      addressLine2: null,
      city: null,
      stateId: null,
      postalCode: null,
      countryId: this.defaultCountryId,
      phoneNumber: null,
      mobileNumber: null,
      email: null,
      website: null,
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
