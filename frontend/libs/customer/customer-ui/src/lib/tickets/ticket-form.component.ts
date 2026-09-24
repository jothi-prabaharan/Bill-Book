import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, TicketPriority } from '@bill-book/customer-core';
import {
  BbSelectOption,
  FormFieldComponent,
  LookupDialogComponent,
  LookupRow,
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
    FormFieldComponent,
    LookupDialogComponent,
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

  /** The chosen contact as it reads on the form; the id itself is the `contactId` control. */
  readonly contactLabel = signal('');

  readonly pickerOpen = signal(false);
  readonly pickerRows = signal<LookupRow[]>([]);
  readonly pickerLoading = signal(false);

  /** Only the latest search may write the rows, so a slow old answer never replaces a newer one. */
  private searchToken = 0;

  /**
   * Opens the contact picker (TK-16). A search over Master's contact list,
   * already scoped to the caller's branch — in place of a dropdown that loaded
   * every contact in the branch before it could show one.
   */
  openContactPicker(): void {
    this.pickerOpen.set(true);
    void this.searchContacts('');
  }

  async searchContacts(term: string): Promise<void> {
    const token = ++this.searchToken;
    this.pickerLoading.set(true);

    try {
      const contacts = await this.customerService.searchContacts(term.trim());

      if (token === this.searchToken) {
        this.pickerRows.set(
          contacts.map((contact) => ({
            id: contact.contactId,
            code: contact.contactCode,
            name: contact.displayName,
            meta: contact.gstin,
          })),
        );
      }
    } catch {
      if (token === this.searchToken) {
        this.pickerRows.set([]);
      }
    } finally {
      if (token === this.searchToken) {
        this.pickerLoading.set(false);
      }
    }
  }

  chooseContact(row: LookupRow): void {
    this.contactLabel.set(`${row.code} ${row.name}`.trim());
    this.form.controls.contactId.setValue(row.id);
    this.form.controls.contactId.markAsTouched();
    this.closePicker();
  }

  closePicker(): void {
    this.searchToken++;
    this.pickerOpen.set(false);
    this.pickerRows.set([]);
    this.pickerLoading.set(false);
  }

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
