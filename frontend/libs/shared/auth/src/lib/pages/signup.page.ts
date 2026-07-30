import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Country, StateRow } from '../auth.models';
import { AuthService } from '../auth.service';

/**
 * Public trial signup. On submit shows the "setting up your account" state and
 * polls customer status until CanLogin — provisioning creates a physical
 * database, so this is eventually consistent.
 */
@Component({
  selector: 'bb-signup-page',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="auth-card wide">
      <h1>Start your 14-day free trial</h1>

      @if (provisioning()) {
        <div class="provisioning">
          <p>Setting up your account…</p>
          <p class="hint">We are creating your database and books. This takes a moment.</p>
        </div>
      } @else {
        <form (ngSubmit)="submit()">
          <fieldset>
            <legend>You</legend>
            <label>Name <input name="displayName" [(ngModel)]="m.displayName" required /></label>
            <label>Email <input name="email" type="email" [(ngModel)]="m.email" required /></label>
            <label>Mobile <input name="mobileNumber" [(ngModel)]="m.mobileNumber" /></label>
            <label>Password <input name="password" type="password" minlength="8" [(ngModel)]="m.password" required /></label>
          </fieldset>

          <fieldset>
            <legend>Company</legend>
            <label>Company name <input name="companyName" [(ngModel)]="m.companyName" required /></label>
            <label>Organization name <input name="organizationName" [(ngModel)]="m.organizationName" required /></label>
            <label>
              Financial year starts
              <select name="fyStart" [(ngModel)]="m.financialYearStartMonth">
                @for (month of months; track month.value) {
                  <option [ngValue]="month.value">{{ month.name }}</option>
                }
              </select>
            </label>
          </fieldset>

          <fieldset>
            <legend>Location</legend>
            <label>
              Country
              <select name="countryId" [(ngModel)]="m.countryId" (ngModelChange)="loadStates($event)" required>
                @for (c of countries(); track c.countryId) {
                  <option [ngValue]="c.countryId">{{ c.countryName }}</option>
                }
              </select>
            </label>
            <label>
              State
              <select name="stateId" [(ngModel)]="m.stateId">
                <option [ngValue]="undefined">—</option>
                @for (s of states(); track s.stateId) {
                  <option [ngValue]="s.stateId">{{ s.stateName }}</option>
                }
              </select>
            </label>
            <label>City <input name="city" [(ngModel)]="m.city" /></label>
          </fieldset>

          <fieldset>
            <legend>Statutory (optional)</legend>
            <label>GSTIN <input name="gstin" maxlength="15" [(ngModel)]="m.gstin" /></label>
            <label>PAN <input name="pan" maxlength="10" [(ngModel)]="m.pan" /></label>
            <label>TAN <input name="tan" maxlength="10" [(ngModel)]="m.tan" /></label>
            <label>TIN <input name="tin" maxlength="15" [(ngModel)]="m.tin" /></label>
            <label>CIN <input name="cin" maxlength="21" [(ngModel)]="m.cin" /></label>
            <label>Udyam <input name="udyamNumber" maxlength="20" [(ngModel)]="m.udyamNumber" /></label>
          </fieldset>

          @if (error()) {
            <p class="error">{{ error() }}</p>
          }

          <button type="submit" [disabled]="busy()">Create my account</button>
        </form>
        <p>Already have an account? <a routerLink="/login">Sign in</a></p>
      }
    </div>
  `,
})
export class SignupPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly countries = signal<Country[]>([]);
  protected readonly states = signal<StateRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly provisioning = signal(false);
  protected readonly error = signal<string | null>(null);

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
    countryId: 1, stateId: undefined as number | undefined, city: '', postalCode: '',
  };

  async ngOnInit(): Promise<void> {
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

  async submit(): Promise<void> {
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
