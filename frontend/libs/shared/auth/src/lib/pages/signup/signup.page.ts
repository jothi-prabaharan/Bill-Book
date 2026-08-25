import { ChangeDetectionStrategy } from '@angular/core';
import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Country, StateRow, Currency } from '../../auth.models';
import { AuthService } from '../../auth.service';

/**
 * Public trial signup. On submit shows the "setting up your account" state and
 * polls customer status until CanLogin — provisioning creates a physical
 * database, so this is eventually consistent.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-signup-page',
  standalone: true,
  imports: [AuthShellComponent, ReactiveFormsModule, RouterLink],
  templateUrl: './signup.page.html',
  styleUrl: './signup.page.scss',
})
export class SignupPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly countries = signal<Country[]>([]);
  protected readonly states = signal<StateRow[]>([]);
  protected readonly currencies = signal<Currency[]>([]);
  
  protected readonly busy = signal(false);
  protected readonly provisioning = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly stepCount = 3;
  protected readonly step = signal(1);

  protected readonly signupForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    lastName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    mobileNumber: new FormControl(''),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
    confirmPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(8)] }),
    
    companyName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    organizationName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    financialYearStartMonth: new FormControl(4, { nonNullable: true, validators: Validators.required }),
    countryId: new FormControl<number | undefined>(undefined, { nonNullable: true, validators: Validators.required }),
    stateId: new FormControl<number | undefined>(undefined),
    addressLine1: new FormControl(''),
    addressLine2: new FormControl(''),
    city: new FormControl(''),
    postalCode: new FormControl(''),
    baseCurrency: new FormControl<string | undefined>(undefined),
    
    gstin: new FormControl(''),
    pan: new FormControl(''),
    tan: new FormControl(''),
    tin: new FormControl(''),
    cin: new FormControl(''),
    udyamNumber: new FormControl(''),
  });

  protected progress(): number {
    return Math.round((this.step() / this.stepCount) * 100);
  }

  next(): void {
    let valid = true;
    if (this.step() === 1) {
      const step1Controls = ['firstName', 'lastName', 'email', 'password', 'confirmPassword'] as const;
      for (const ctrl of step1Controls) {
        if (this.signupForm.controls[ctrl].invalid) {
          this.signupForm.controls[ctrl].markAsTouched();
          valid = false;
        }
      }
      if (!valid) {
        this.error.set('Please fill out all required fields.');
        return;
      }
      if (this.signupForm.value.password !== this.signupForm.value.confirmPassword) {
        this.error.set('Both passwords must match.');
        return;
      }
    } else if (this.step() === 2) {
      const step2Controls = ['companyName', 'organizationName', 'financialYearStartMonth', 'countryId'] as const;
      for (const ctrl of step2Controls) {
        if (this.signupForm.controls[ctrl].invalid) {
          this.signupForm.controls[ctrl].markAsTouched();
          valid = false;
        }
      }
      if (!valid) {
        this.error.set('Please fill out all required fields.');
        return;
      }
    }

    this.error.set(null);
    this.step.update((s) => Math.min(s + 1, this.stepCount));
  }

  back(): void {
    if (this.step() === 1) {
      void this.router.navigateByUrl('/login');
      return;
    }
    this.error.set(null);
    this.step.update((s) => s - 1);
  }

  protected readonly months = [
    { value: 1, name: 'January' }, { value: 2, name: 'February' }, { value: 3, name: 'March' },
    { value: 4, name: 'April' }, { value: 5, name: 'May' }, { value: 6, name: 'June' },
    { value: 7, name: 'July' }, { value: 8, name: 'August' }, { value: 9, name: 'September' },
    { value: 10, name: 'October' }, { value: 11, name: 'November' }, { value: 12, name: 'December' },
  ];

  ngOnInit(): void {
    void this.load();
    this.signupForm.controls.countryId.valueChanges.subscribe((id) => {
      void this.loadStates(id);
    });
  }

  private async load(): Promise<void> {
    try {
      const fetchedCountries = await this.auth.countries();
      this.countries.set(fetchedCountries);
      if (!this.signupForm.value.countryId && fetchedCountries.length > 0) {
        this.signupForm.patchValue({ countryId: fetchedCountries[0].countryId });
      }
      
      const fetchedCurrencies = await this.auth.currencies();
      this.currencies.set(fetchedCurrencies);
      
      await this.loadStates(this.signupForm.value.countryId);
    } catch {
      this.error.set('Could not load reference data. Is the Master service running?');
    }
  }

  async loadStates(countryId: number | undefined): Promise<void> {
    this.signupForm.patchValue({ stateId: undefined });
    if (!countryId) {
      this.states.set([]);
      this.signupForm.patchValue({ baseCurrency: undefined });
      return;
    }
    const country = this.countries().find((c) => c.countryId === countryId);
    this.signupForm.patchValue({ baseCurrency: country?.currencyCode });
    this.states.set(await this.auth.states(countryId));
  }

  async submit(): Promise<void> {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      this.error.set('Please fill out all required fields.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    
    const val = this.signupForm.getRawValue();
    const payload = {
      displayName: `${val.firstName} ${val.lastName}`.trim(),
      email: val.email,
      password: val.password,
      mobileNumber: val.mobileNumber ?? '',
      companyName: val.companyName,
      organizationName: val.organizationName,
      financialYearStartMonth: val.financialYearStartMonth,
      baseCurrency: val.baseCurrency ?? undefined,
      gstin: val.gstin ?? '',
      pan: val.pan ?? '',
      tan: val.tan ?? '',
      tin: val.tin ?? '',
      cin: val.cin ?? '',
      udyamNumber: val.udyamNumber ?? '',
      countryId: val.countryId ?? 1,
      stateId: val.stateId ?? undefined,
      addressLine1: val.addressLine1 ?? '',
      addressLine2: val.addressLine2 ?? '',
      city: val.city ?? '',
      postalCode: val.postalCode ?? '',
    };
    
    try {
      const response = await this.auth.signup(payload);
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

