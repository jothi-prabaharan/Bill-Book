import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface StateRow {
  stateId: number;
  stateCode: string;
  stateName: string;
}

interface Currency {
  currencyId: number;
  code: string;
  name: string;
}

interface OrganizationSettings {
  orgId: string;
  orgCode: string;
  name: string;
  baseCurrency: string;
  financialYearStartMonth: number;
  allowFreeTextLines: boolean;
  discountLevel: 'Line' | 'Header' | 'Both';
  discountBeforeTax: boolean;
  gstin: string | null;
  pan: string | null;
  tan: string | null;
  tin: string | null;
  cin: string | null;
  udyamNumber: string | null;
  logoUrl: string | null;
  expiryDate: string | null;
  isTrial: boolean;
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
}

type Tab = 'profile' | 'statutory' | 'financial';

const MONTHS = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/**
 * Settings › Organization — the branch you are signed in to, not a list.
 *
 * It exists because seven columns were reachable from nowhere: TAN, TIN, CIN,
 * Udyam number, logo, website and country were settable at signup and frozen
 * afterwards. An Indian business corrects those — a CIN arrives when a firm
 * incorporates, a Udyam registration when it registers as an MSME.
 *
 * **Branch, not account.** The head office's licence and billing are not here;
 * this edits one set of books. Which branch is taken from the token rather than
 * the URL, so it is always the one being worked in and never one the user is
 * not signed into.
 *
 * The base currency is deliberately read-only: every posting in the branch
 * converts to it, so changing it after anything has been posted would restate
 * the books. It is chosen when the branch is created and fixed thereafter.
 */
@Component({
  selector: 'bb-organization-settings-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './organization-settings.page.html',
  styleUrl: './organization-settings.page.scss',
})
export class OrganizationSettingsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly tab = signal<Tab>('profile');
  protected readonly busy = signal(false);
  protected readonly loaded = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly states = signal<StateRow[]>([]);
  protected readonly currencies = signal<Currency[]>([]);

  protected readonly months = MONTHS;

  form: OrganizationSettings | null = null;

  /** The state whose code must match the GSTIN's first two digits. */
  protected readonly gstinStateCode = computed(() => {
    const gstin = this.form?.gstin?.trim() ?? '';
    return gstin.length >= 2 ? gstin.slice(0, 2) : null;
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    try {
      this.form = await this.get<OrganizationSettings>('/api/organizations/current');
      this.loaded.set(true);
      void this.loadStates(this.form.countryId);
      void this.loadCurrencies();
    } catch {
      this.error.set('Could not load this branch.');
    } finally {
      this.busy.set(false);
    }
  }

  async save(): Promise<void> {
    if (!this.form) {
      return;
    }

    if (
      !this.hasText(this.form.orgCode) ||
      !this.hasText(this.form.name) ||
      !this.hasText(this.form.baseCurrency)
    ) {
      this.error.set('Branch code, name and base currency are required.');
      return;
    }

    if (this.form.financialYearStartMonth < 1 || this.form.financialYearStartMonth > 12) {
      this.error.set('Financial year start month must be between 1 and 12.');
      return;
    }

    const body: OrganizationSettings = {
      ...this.form,
      orgCode: this.form.orgCode.trim(),
      name: this.form.name.trim(),
      baseCurrency: this.form.baseCurrency.trim().toUpperCase(),
    };

    this.busy.set(true);
    this.error.set(null);
    this.saved.set(false);

    try {
      await this.put('/api/organizations/current', body);
      this.saved.set(true);
    } catch (error: unknown) {
      const body = (error as { error?: { message?: string } })?.error;
      this.error.set(body?.message ?? 'Could not save. Check the values and try again.');
    } finally {
      this.busy.set(false);
    }
  }

  /**
   * Warns before the server refuses. The GSTIN's first two digits are the state
   * code, and a mismatch is rejected on save — saying so while the field is
   * still in front of the user beats a message after a round trip.
   */
  protected readonly gstinMismatch = computed(() => {
    const prefix = this.gstinStateCode();
    const stateId = this.form?.stateId ?? null;
    if (prefix === null || stateId === null) {
      return false;
    }

    const state = this.states().find((s) => s.stateId === stateId);
    return state !== undefined && state.stateCode !== prefix;
  });

  private async loadStates(countryId: number): Promise<void> {
    try {
      this.states.set(await this.get<StateRow[]>(`/api/master/countries/${countryId}/states`));
    } catch {
      this.states.set([]);
    }
  }

  private async loadCurrencies(): Promise<void> {
    try {
      this.currencies.set(await this.get<Currency[]>('/api/master/currencies'));
    } catch {
      this.currencies.set([]);
    }
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) => {
      this.http.get<T>(url).subscribe({ next: resolve, error: reject });
    });
  }

  private put(url: string, body: unknown): Promise<unknown> {
    return new Promise((resolve, reject) => {
      this.http.put(url, body).subscribe({ next: resolve, error: reject });
    });
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }
}
