import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, LeadSource } from '@bill-book/customer-core';
import {
  BbSelectOption,
  bbEmail,
  EmailInputComponent,
  MessageBoxComponent,
  PhoneInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-lead-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TextInputComponent,
    EmailInputComponent,
    PhoneInputComponent,
    SelectComponent,
    MessageBoxComponent,
  ],
  templateUrl: './lead-form.component.html',
  styleUrl: './lead-form.component.scss'
})
export class LeadFormComponent {
  private readonly customerService = inject(CustomerService);

  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly sources = Object.values(LeadSource);

  /** The same list, in the shape `bb-select` takes. */
  readonly sourceOptions: BbSelectOption<LeadSource>[] = this.sources.map((source) => ({
    value: source,
    label: source,
  }));

  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: Validators.required }),
    companyName: new FormControl('', { nonNullable: true }),
    // `bbEmail` rather than `Validators.email`: it is the same expression the
    // field renders as its HTML `pattern`, so the screen and the validator
    // cannot disagree about what an address is.
    email: new FormControl('', { nonNullable: true, validators: bbEmail() }),
    phone: new FormControl('', { nonNullable: true }),
    source: new FormControl<LeadSource>(LeadSource.Website, { nonNullable: true, validators: Validators.required })
  });

  /** Whether a field should show its error yet — touched, and actually wrong. */
  protected showError(control: keyof typeof this.form.controls): boolean {
    const field = this.form.controls[control];
    return field.invalid && (field.touched || field.dirty);
  }

  async save() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.customerService.createLead(this.form.getRawValue());
      this.saved.emit();
    } catch (err: unknown) {
      const failure = err as { error?: { message?: string } };
      this.errorMessage.set(failure?.error?.message || 'Failed to save lead. Please try again.');
      console.error('Failed to save lead', err);
    } finally {
      this.saving.set(false);
    }
  }
}
