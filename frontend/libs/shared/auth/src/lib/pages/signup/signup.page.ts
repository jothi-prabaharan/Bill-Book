import { ChangeDetectionStrategy } from '@angular/core';
import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Country, StateRow, Currency } from '../../auth.models';
import { AuthService } from '../../auth.service';
import { ADD_ONS, AddOnCode, BUSINESS_TYPES, isAddOnAllowed } from '../../business-types';
import {
  BbSelectOption,
  CheckboxComponent,
  EmailInputComponent,
  MessageBoxComponent,
  PasswordInputComponent,
  PhoneInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

/**
 * Public trial signup. On submit shows the "setting up your account" state and
 * polls customer status until CanLogin — provisioning seeds the new
 * organization's master data (chart of accounts, tax master, units...), so
 * this is eventually consistent even though there is no longer a database to
 * create first.
 *
 * The first branch's business type and add-ons are chosen here (D-50, TK-136):
 * there are no apps to sign up for any more. "HRMS or Payroll only" sends no
 * business type, and then one of those two add-ons is required.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-signup-page',
  standalone: true,
  imports: [
    AuthShellComponent,
    ReactiveFormsModule,
    RouterLink,
    TextInputComponent,
    EmailInputComponent,
    PhoneInputComponent,
    PasswordInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
  ],
  templateUrl: './signup.page.html',
  styleUrl: './signup.page.scss',
})
export class SignupPage implements OnInit {

  /**
   * A getter, not a field: `months` is declared further down the class, and a
   * field initialiser reading it would run before it exists.
   */
  protected get monthOptions(): BbSelectOption<number>[] {
    return this.months.map((month) => ({ value: month.value, label: month.name }));
  }

  protected readonly countryOptions = computed<BbSelectOption<number>[]>(() =>
    this.countries().map((country) => ({
      value: country.countryId,
      label: country.countryName,
    })),
  );

  protected readonly stateOptions = computed<BbSelectOption<number>[]>(() =>
    this.states().map((state) => ({ value: state.stateId, label: state.stateName })),
  );

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.currencies().map((currency) => ({
      value: currency.code,
      label: `${currency.code} — ${currency.name}`,
    })),
  );
  /** `''` is "HRMS or Payroll only": a customer with no trade. */
  protected readonly businessTypeOptions: BbSelectOption<string>[] = [
    ...BUSINESS_TYPES.map((t) => ({ value: t.value as string, label: t.label })),
    { value: '', label: 'HRMS or Payroll only' },
  ];

  protected readonly addOns = ADD_ONS;

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

    businessType: new FormControl('Retail', { nonNullable: true }),
    addOns: new FormGroup(
      Object.fromEntries(ADD_ONS.map((a) => [a.value, new FormControl(false, { nonNullable: true })])) as Record<
        AddOnCode,
        FormControl<boolean>
      >,
    ),
  });

  /** The add-ons ticked and allowed for the chosen business type, as the server reads a flags enum. */
  protected chosenAddOns(): string {
    const type = this.signupForm.controls.businessType.value || null;
    const ticked = ADD_ONS.map((a) => a.value).filter(
      (code) => this.signupForm.controls.addOns.controls[code].value && isAddOnAllowed(type, code),
    );
    return ticked.length > 0 ? ticked.join(', ') : 'None';
  }

  /** Disables, and clears, each add-on the business type does not allow. */
  private applyAddOnRules(type: string): void {
    for (const { value: code } of ADD_ONS) {
      const control = this.signupForm.controls.addOns.controls[code];
      if (isAddOnAllowed(type || null, code)) {
        control.enable({ emitEvent: false });
      } else {
        control.setValue(false, { emitEvent: false });
        control.disable({ emitEvent: false });
      }
    }
  }

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
      if (!this.signupForm.controls.businessType.value && this.chosenAddOns() === 'None') {
        this.error.set('Choose HRMS or Payroll, or pick a business type.');
        return;
      }
    }

    this.error.set(null);
    this.step.update((s) => Math.min(s + 1, this.stepCount));
  }

  back(): void {
    if (this.step() === 1) {
      if (typeof window !== 'undefined' && window.history.length > 1) {
        window.history.back();
        return;
      }
      void this.router.navigateByUrl('/');
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
    this.applyAddOnRules(this.signupForm.controls.businessType.value);
    this.signupForm.controls.businessType.valueChanges.subscribe((type) => this.applyAddOnRules(type));
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
      businessType: val.businessType || null,
      addOns: this.chosenAddOns(),
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
      if (status.provisioningStatus === 'Failed') {
        throw new Error('Provisioning failed');
      }
      await new Promise((resolve) => setTimeout(resolve, 2000));
    }
  }
}

