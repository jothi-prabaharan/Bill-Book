import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Country, StateRow } from '../../auth.models';
import { AuthService } from '../../auth.service';

/**
 * Public trial signup. On submit shows the "setting up your account" state and
 * polls customer status until CanLogin — provisioning creates a physical
 * database, so this is eventually consistent.
 */
@Component({
  selector: 'bb-signup-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './signup.page.html',
  styleUrl: './signup.page.scss',
})
export class SignupPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly countries = signal<Country[]>([]);
  protected readonly states = signal<StateRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly provisioning = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly currentTab = signal<number>(0);
  protected readonly tabs = ['Personal', 'Company', 'Location', 'Statutory'];

  nextTab(): void {
    if (this.currentTab() < this.tabs.length - 1) {
      this.currentTab.update(v => v + 1);
    }
  }

  prevTab(): void {
    if (this.currentTab() > 0) {
      this.currentTab.update(v => v - 1);
    }
  }

  protected readonly months = [
    { value: 1, name: 'January' }, { value: 2, name: 'February' }, { value: 3, name: 'March' },
    { value: 4, name: 'April' }, { value: 5, name: 'May' }, { value: 6, name: 'June' },
    { value: 7, name: 'July' }, { value: 8, name: 'August' }, { value: 9, name: 'September' },
    { value: 10, name: 'October' }, { value: 11, name: 'November' }, { value: 12, name: 'December' },
  ];

  m = {
    displayName: '', email: '', password: '', mobileNumber: '',
    companyName: '', organizationName: '', financialYearStartMonth: 4,
    baseCurrency: undefined as string | undefined,
    gstin: '', pan: '', tan: '', tin: '', cin: '', udyamNumber: '',
    countryId: 1, stateId: undefined as number | undefined,
    addressLine1: '', addressLine2: '', city: '', postalCode: '',
  };

  ngOnInit(): void {
    // Angular does not await ngOnInit, so returning a promise from it means
    // nothing is watching for a rejection. The work is kicked off explicitly
    // instead, and load() handles its own failure.
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.countries.set(await this.auth.countries());
      await this.loadStates(this.m.countryId);
    } catch {
      this.error.set('Could not load reference data. Is the Master service running?');
    }
  }

  async loadStates(countryId: number): Promise<void> {
    this.m.stateId = undefined;
    const country = this.countries().find((c) => c.countryId === countryId);
    this.m.baseCurrency = country?.currencyCode;
    this.states.set(countryId ? await this.auth.states(countryId) : []);
  }

  async submit(form: NgForm): Promise<void> {
    if (form.invalid) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const response = await this.auth.signup({ ...this.m });
      this.provisioning.set(true);
      await this.poll(response.customerId);
      await this.router.navigateByUrl('/login');
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.error.set(anyErr?.error?.message ?? 'Signup failed. Please try again.');
      this.provisioning.set(false);
    } finally {
      this.busy.set(false);
    }
  }

  private async poll(customerId: string): Promise<void> {
    // Poll until the tenant database is ready; login is blocked until then.
    for (;;) {
      const status = await this.auth.customerStatus(customerId);
      if (status.canLogin) {
        return;
      }
      if (status.databaseStatus === 'Failed') {
        throw new Error('Provisioning failed');
      }
      await new Promise((resolve) => setTimeout(resolve, 2000));
    }
  }
}
