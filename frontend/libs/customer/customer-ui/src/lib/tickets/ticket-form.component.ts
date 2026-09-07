import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, TicketPriority } from '@bill-book/customer-core';
import {
  BbSelectOption,
  MasterSelectComponent,
  MessageBoxComponent,
  SelectComponent,
  TextareaComponent,
  TextInputComponent,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-form',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MasterSelectComponent,
    TextInputComponent,
    TextareaComponent,
    SelectComponent,
    MessageBoxComponent,
  ],
  templateUrl: './ticket-form.component.html',
  styleUrl: './ticket-form.component.scss'
})
export class TicketFormComponent {
  private readonly customerService = inject(CustomerService);

  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly priorities = Object.values(TicketPriority);

  /** The same list, in the shape `bb-select` takes. */
  readonly priorityOptions: BbSelectOption<TicketPriority>[] = this.priorities.map((priority) => ({
    value: priority,
    label: priority,
  }));

  readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: Validators.required }),
    description: new FormControl('', { nonNullable: true, validators: Validators.required }),
    priority: new FormControl<TicketPriority>(TicketPriority.Medium, { nonNullable: true, validators: Validators.required }),
    contactId: new FormControl<number | null>(null, { validators: Validators.required })
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
      const { subject, description, priority, contactId } = this.form.getRawValue();
      await this.customerService.createTicket({
        subject,
        description,
        priority,
        // Guarded by `Validators.required` above, so it is never null here.
        contactId: contactId as number,
      });
      this.saved.emit();
    } catch (err: unknown) {
      const failure = err as { error?: { message?: string } };
      this.errorMessage.set(failure?.error?.message || 'Failed to save ticket.');
      console.error('Failed to save ticket', err);
    } finally {
      this.saving.set(false);
    }
  }
}
