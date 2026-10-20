import { ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { AuthShellComponent } from '../../components/auth-shell/auth-shell.component';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Country, StateRow, Currency, OtpChannel, SignupCodeResponse, SignupRequest } from '../../auth.models';
import { AuthService } from '../../auth.service';
import { ADD_ONS, AddOnCode, BUSINESS_TYPES, isAddOnAllowed } from '../../business-types';
import {
  DEFAULT_OTP_CHANNEL,
  OTP_CHANNELS,
  OTP_CODE_PATTERN,
  apiMessage,
  channelPhrase,
  fieldError,
  formatWait,
  isPhoneChannel,
  isSignupCodeSent,
  retryAfterSeconds,
  secondsLeft,
} from '../../otp-flow';
import {
  BbSelectOption,
  CheckboxComponent,
  EmailInputComponent,
  MessageBoxComponent,
  PasswordInputComponent,
  PhoneInputComponent,
  RadioGroupComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
  bbPhone,
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
 *
 * **Nothing is created until a code proves the email or the mobile number**
 * (TK-196, TK-197). The fourth step sends a six-digit code by email (chosen
 * unless the person picks WhatsApp or SMS, which need the mobile number), and
 * the same signup sent again with that code is what creates the trial. A
 * field's own problem shows under the field; what the server says — a wrong or
 * expired code, a code that could not be sent — shows in the message box.
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
    RadioGroupComponent,
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
  private readonly destroyRef = inject(DestroyRef);

  protected readonly countries = signal<Country[]>([]);
  protected readonly states = signal<StateRow[]>([]);
  protected readonly currencies = signal<Currency[]>([]);
  
  protected readonly busy = signal(false);
  protected readonly provisioning = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly stepCount = 4;
  protected readonly step = signal(1);

  // ---- Step 4: the code (TK-196, TK-197) ----------------------------------

  protected readonly channelOptions = OTP_CHANNELS;

  protected readonly verifyForm = new FormGroup({
    otpChannel: new FormControl<OtpChannel>(DEFAULT_OTP_CHANNEL, { nonNullable: true }),
    otpCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(OTP_CODE_PATTERN)],
    }),
  });

  /** The chosen channel, as a signal for the template's words. */
  protected readonly channel = signal<OtpChannel>(DEFAULT_OTP_CHANNEL);
  protected readonly byPhone = computed(() => isPhoneChannel(this.channel()));
  protected readonly channelWords = computed(() => channelPhrase(this.channel()));

  /** Where the last code went; null until one has been sent for the address or number on the form. */
  protected readonly codeSent = signal<SignupCodeResponse | null>(null);
  protected readonly sending = signal(false);

  /** When another code may be asked for (epoch ms), and a clock that ticks while waiting. */
  private readonly resendAt = signal<number | null>(null);
  private readonly now = signal(Date.now());
  protected readonly resendIn = computed(() => secondsLeft(this.resendAt(), this.now()));
  protected readonly resendWait = computed(() => formatWait(this.resendIn()));

  /** What the message box shows: the server's refusal, else where the code went. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.error();
    if (failure) {
      return [{ tone: 'error', text: failure }];
    }
    const sent = this.codeSent();
    return sent && this.step() === this.stepCount
      ? [{ tone: 'success', text: `Code sent ${channelPhrase(sent.channel)} to ${sent.destination}. It works for ${Math.round(sent.expiresInSeconds / 60)} minutes.` }]
      : [];
  });

  protected readonly signupForm = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    lastName: new FormControl('', { nonNullable: true, validators: Validators.required }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    // Required only for a code on WhatsApp or by SMS; see applyChannel.
    mobileNumber: new FormControl('', { nonNullable: true, validators: [bbPhone()] }),
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

  /**
   * The mobile number is needed only when the code goes to it, and a code sent
   * one way does not work another, so a new channel starts the code over.
   */
  private applyChannel(channel: OtpChannel): void {
    this.channel.set(channel);
    const mobile = this.signupForm.controls.mobileNumber;
    mobile.setValidators(isPhoneChannel(channel) ? [Validators.required, bbPhone()] : [bbPhone()]);
    mobile.updateValueAndValidity({ emitEvent: false });
    this.error.set(null);
    this.restartCode();
  }

  /** Forgets the code sent: it no longer matches where the next one would go. */
  private restartCode(): void {
    this.codeSent.set(null);
    this.resendAt.set(null);
    this.verifyForm.controls.otpCode.reset('');
  }

  /** The words under a field once it has been touched; empty when it is fine. */
  protected err(control: AbstractControl, label: string, words: Partial<Record<string, string>> = {}): string {
    return fieldError(control, label, words);
  }

  private static readonly stepControls: Record<number, readonly string[]> = {
    1: ['firstName', 'lastName', 'email', 'mobileNumber', 'password', 'confirmPassword'],
    2: ['companyName', 'organizationName', 'financialYearStartMonth', 'countryId'],
    3: ['gstin', 'pan', 'tan', 'tin', 'cin', 'udyamNumber'],
  };

  /** Touches a step's fields so each shows its own problem; true when the step is fine. */
  private checkStep(step: number): boolean {
    let valid = true;
    for (const name of SignupPage.stepControls[step] ?? []) {
      const control = this.signupForm.get(name);
      if (control?.invalid) {
        control.markAsTouched();
        valid = false;
      }
    }

    if (step === 1 && valid && this.signupForm.value.password !== this.signupForm.value.confirmPassword) {
      const confirm = this.signupForm.controls.confirmPassword;
      confirm.setErrors({ mismatch: true });
      confirm.markAsTouched();
      valid = false;
    }

    return valid;
  }

  next(): void {
    this.error.set(null);
    if (!this.checkStep(this.step())) {
      return;
    }

    if (this.step() === 2 && !this.signupForm.controls.businessType.value && this.chosenAddOns() === 'None') {
      this.error.set('Choose HRMS or Payroll, or pick a business type.');
      return;
    }

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

  constructor() {
    // The resend countdown. One tick a second is all it needs, and only this
    // page reads it.
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  ngOnInit(): void {
    // A code proves one address or number: change it and it has to be sent again.
    this.signupForm.controls.email.valueChanges.subscribe(() => this.restartCode());
    this.signupForm.controls.mobileNumber.valueChanges.subscribe(() => this.restartCode());
    this.verifyForm.controls.otpChannel.valueChanges.subscribe((channel) => this.applyChannel(channel));
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
        // India unless it is missing: the list is alphabetical, and its first
        // entry would give the verification code the wrong country code.
        const preferred = fetchedCountries.find((c) => c.countryCode === 'IN') ?? fetchedCountries[0];
        this.signupForm.patchValue({ countryId: preferred.countryId });
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

  /** The signup as the server reads it, with or without the code. */
  private payload(otpCode?: string): SignupRequest {
    const val = this.signupForm.getRawValue();
    return {
      businessType: val.businessType || null,
      addOns: this.chosenAddOns(),
      displayName: `${val.firstName} ${val.lastName}`.trim(),
      email: val.email,
      password: val.password,
      mobileNumber: val.mobileNumber.trim() || undefined,
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
      otpChannel: this.verifyForm.controls.otpChannel.value,
      otpCode,
    };
  }

  /** Back to the first step with a problem, every field on it showing its own. False when there is one. */
  private everyStepFine(): boolean {
    for (let step = 1; step < this.stepCount; step++) {
      if (!this.checkStep(step)) {
        this.step.set(step);
        return false;
      }
    }
    return true;
  }

  /** Sends a code by the chosen channel. Nothing is created by this. */
  async sendCode(): Promise<void> {
    if (this.sending() || this.resendIn() > 0) {
      return;
    }

    // The mobile field is on this step too when the code goes to it, so its
    // problem shows here rather than sending the person back to the first step.
    const mobile = this.signupForm.controls.mobileNumber;
    if (this.byPhone() && mobile.invalid) {
      mobile.markAsTouched();
      return;
    }

    if (!this.everyStepFine()) {
      return;
    }

    this.sending.set(true);
    this.error.set(null);
    try {
      const response = await this.auth.signup(this.payload());
      if (isSignupCodeSent(response)) {
        this.codeSent.set(response);
        this.verifyForm.controls.otpCode.reset('');
        this.resendAt.set(Date.now() + response.resendAfterSeconds * 1000);
      }
    } catch (err: unknown) {
      const wait = retryAfterSeconds(err);
      if (wait) {
        this.resendAt.set(Date.now() + wait * 1000);
      }
      this.error.set(apiMessage(err, 'The code could not be sent. Please try again.'));
    } finally {
      this.sending.set(false);
    }
  }

  /** Sends the signup with its code; the right code creates the trial. */
  async submit(): Promise<void> {
    if (this.step() < this.stepCount) {
      this.next();
      return;
    }

    if (!this.codeSent()) {
      await this.sendCode();
      return;
    }

    const code = this.verifyForm.controls.otpCode;
    if (code.invalid) {
      code.markAsTouched();
      return;
    }

    if (!this.everyStepFine()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    try {
      const response = await this.auth.signup(this.payload(code.value));
      if (isSignupCodeSent(response)) {
        // Not expected with a code, but it is a code on its way, not a failure.
        this.codeSent.set(response);
        return;
      }
      this.provisioning.set(true);
      await this.poll(response.customerId);
      await this.router.navigateByUrl('/login');
    } catch (err: unknown) {
      this.error.set(apiMessage(err, 'Signup failed. Please try again.'));
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

