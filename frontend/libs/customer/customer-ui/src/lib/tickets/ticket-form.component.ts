import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { CustomerService, TicketPriority } from '@bill-book/customer-core';
import { MasterSelectComponent, TextInputComponent, MessageBoxComponent } from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-ticket-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MasterSelectComponent, TextInputComponent, MessageBoxComponent],
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

  readonly form = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: Validators.required }),
    description: new FormControl('', { nonNullable: true, validators: Validators.required }),
    priority: new FormControl<TicketPriority>(TicketPriority.Medium, { nonNullable: true, validators: Validators.required }),
    contactId: new FormControl<number | null>(null, { validators: Validators.required })
  });

  async save() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    this.errorMessage.set(null);
    try {
      await this.customerService.createTicket(this.form.getRawValue() as any);
      this.saved.emit();
    } catch (err: any) {
      this.errorMessage.set(err?.error?.message || 'Failed to save ticket.');
      console.error('Failed to save ticket', err);
    } finally {
      this.saving.set(false);
    }
  }
}
